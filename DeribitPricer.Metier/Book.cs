namespace DeribitPricer.Metier;

// Partie 3.2 : suivi du book (valeurs, P&L, grecques), calculé à partir du blotter
public class Book
{
    private Blotter _blotter;

    // Prix de règlement Deribit par jour d'échéance (pour les options échues)
    public Dictionary<DateTime, double> PrixReglement { get; set; }

    public Book(Blotter blotter)
    {
        _blotter = blotter;
        PrixReglement = new Dictionary<DateTime, double>();
    }

    // ---- Valeur d'une option (en BTC) ----

    public bool EstEchue(Option option)
    {
        return option.Echeance <= DateTime.UtcNow;
    }

    // Option échue : payoff en USD divisé par le prix de règlement (annexe A)
    // Sinon : au mark Deribit, ou à notre prix théorique Black-76
    public double ValeurOption(Option option, bool auTheorique)
    {
        if (EstEchue(option) && PrixReglement.ContainsKey(option.Echeance.Date))
        {
            double reglement = PrixReglement[option.Echeance.Date];
            double payoffUsd = Black76.PrixUsd(option.Type, reglement, option.Strike, 0, 0);
            return payoffUsd / reglement;
        }

        if (auTheorique && !EstEchue(option))
        {
            double maturite = Black76.Maturite(option.Echeance, option.Horodatage);
            return Black76.PrixBtc(option.Type, option.Future, option.Strike, maturite, option.MarkIv / 100);
        }

        return option.Mark;
    }

    // ---- Par structure (trade d'origine + ses débouclages) ----

    // Valeur de ce qui reste ouvert
    public double ValeurBtc(Trade origine, bool auTheorique)
    {
        double ratioRestant = origine.QuantiteRestante / origine.Quantite;
        double total = 0;
        foreach (LigneTrade ligne in origine.Lignes)
        {
            total += ligne.QuantiteSignee * ratioRestant * ValeurOption(ligne.Option, auTheorique);
        }
        return total;
    }

    // P&L depuis l'ouverture = valeur actuelle - primes payées (origine + débouclages)
    public double PnlBtc(Trade origine, bool auTheorique)
    {
        double primes = origine.PrimeBtc();
        foreach (Trade debouclage in _blotter.Debouclages(origine))
        {
            primes += debouclage.PrimeBtc();
        }
        return ValeurBtc(origine, auTheorique) - primes;
    }

    // ---- Book total ----

    public double ValeurTotaleBtc(bool auTheorique)
    {
        double total = 0;
        foreach (Trade origine in _blotter.TradesOrigine())
        {
            total += ValeurBtc(origine, auTheorique);
        }
        return total;
    }

    public double PnlTotalBtc(bool auTheorique)
    {
        double total = 0;
        foreach (Trade origine in _blotter.TradesOrigine())
        {
            total += PnlBtc(origine, auTheorique);
        }
        return total;
    }

    // ---- Grecques (positions ouvertes, options non échues) ----

    // Échéances des positions encore ouvertes, triées
    public List<DateTime> EcheancesOuvertes()
    {
        List<DateTime> echeances = new List<DateTime>();
        foreach (Trade origine in _blotter.TradesOrigine())
        {
            if (origine.QuantiteRestante <= 0)
            {
                continue;
            }
            foreach (LigneTrade ligne in origine.Lignes)
            {
                if (!EstEchue(ligne.Option) && !echeances.Contains(ligne.Option.Echeance))
                {
                    echeances.Add(ligne.Option.Echeance);
                }
            }
        }
        echeances.Sort();
        return echeances;
    }

    // Renvoie [delta, gamma, vega, theta] pour une échéance (null = tout le book)
    public double[] Grecques(DateTime? echeance)
    {
        double[] grecques = new double[4];
        foreach (Trade origine in _blotter.TradesOrigine())
        {
            AjouterGrecques(origine, echeance, grecques);
        }
        return grecques;
    }

    // Renvoie [delta, gamma, vega, theta] d'une seule structure (pour les exports)
    public double[] GrecquesStructure(Trade origine)
    {
        double[] grecques = new double[4];
        AjouterGrecques(origine, null, grecques);
        return grecques;
    }

    // Ajoute aux 4 grecques celles des jambes encore ouvertes d'une structure
    private void AjouterGrecques(Trade origine, DateTime? echeance, double[] grecques)
    {
        double ratioRestant = origine.QuantiteRestante / origine.Quantite;

        foreach (LigneTrade ligne in origine.Lignes)
        {
            Option o = ligne.Option;
            if (EstEchue(o))
            {
                continue;   // une option échue n'a plus de risque
            }
            if (echeance != null && o.Echeance != echeance.Value)
            {
                continue;   // pas la bonne échéance
            }

            double quantite = ligne.QuantiteSignee * ratioRestant;
            double maturite = Black76.Maturite(o.Echeance, o.Horodatage);
            double vol = o.MarkIv / 100;

            grecques[0] += quantite * Black76.Delta(o.Type, o.Future, o.Strike, maturite, vol);
            grecques[1] += quantite * Black76.Gamma(o.Future, o.Strike, maturite, vol);
            grecques[2] += quantite * Black76.Vega(o.Future, o.Strike, maturite, vol);
            grecques[3] += quantite * Black76.Theta(o.Future, o.Strike, maturite, vol);
        }
    }
}
