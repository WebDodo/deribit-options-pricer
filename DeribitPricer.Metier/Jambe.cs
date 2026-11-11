namespace DeribitPricer.Metier;

public enum Sens
{
    Achat,
    Vente
}

public class Jambe
{
    public Option Option { get; set; }
    public Sens Sens { get; set; }
    public double Quantite { get; set; }   // toujours positive

    public Jambe(Option option, Sens sens, double quantite)
    {
        Option = option;
        Sens = sens;
        Quantite = quantite;
    }

    // Quantité signée : positive à l'achat, négative à la vente (annexe B)
    public double QuantiteSignee()
    {
        if (Sens == Sens.Achat)
        {
            return Quantite;
        }
        return -Quantite;
    }

    // ---- Calculs pour UNE option de cette jambe (avec ton Black-76) ----

    // Maturité : échéance - horodatage de la cotation Deribit (annexe A)
    public double Maturite()
    {
        return Black76.Maturite(Option.Echeance, Option.Horodatage);
    }

    public double PrixTheoBtc()
    {
        return Black76.PrixBtc(Option.Type, Option.Future, Option.Strike, Maturite(), Option.MarkIv / 100);
    }

    public double PrixTheoUsd()
    {
        return Black76.PrixUsd(Option.Type, Option.Future, Option.Strike, Maturite(), Option.MarkIv / 100);
    }

    // Vol implicite à partir d'un prix en BTC (bid, mark ou ask) ; -1 si impossible
    public double VolImplicite(double prixBtc)
    {
        return Black76.VolImplicite(Option.Type, Option.Future, Option.Strike, Maturite(), prixBtc);
    }

    public double Delta()
    {
        return Black76.Delta(Option.Type, Option.Future, Option.Strike, Maturite(), Option.MarkIv / 100);
    }

    public double Gamma()
    {
        return Black76.Gamma(Option.Future, Option.Strike, Maturite(), Option.MarkIv / 100);
    }

    public double Vega()
    {
        return Black76.Vega(Option.Future, Option.Strike, Maturite(), Option.MarkIv / 100);
    }

    public double Theta()
    {
        return Black76.Theta(Option.Future, Option.Strike, Maturite(), Option.MarkIv / 100);
    }

    public void Afficher()
    {
        Console.WriteLine($"   {Sens,-6} {Quantite,5} x {Option.Nom}");
    }
}
