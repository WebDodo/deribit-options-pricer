namespace DeribitPricer.Metier;

public class Structure
{
    public string Nom { get; set; }
    public List<Jambe> Jambes { get; set; }

    public Structure(string nom)
    {
        Nom = nom;
        Jambes = new List<Jambe>();
    }

    public void AjouterJambe(Option option, Sens sens, double quantite)
    {
        Jambe jambe = new Jambe(option, sens, quantite);
        Jambes.Add(jambe);
    }

    // ---- Prix ----

    public double PrixTheoBtc()
    {
        double total = 0;
        foreach (Jambe jambe in Jambes)
        {
            total += jambe.QuantiteSignee() * jambe.PrixTheoBtc();
        }
        return total;
    }

    public double PrixTheoUsd()
    {
        double total = 0;
        foreach (Jambe jambe in Jambes)
        {
            total += jambe.QuantiteSignee() * jambe.PrixTheoUsd();
        }
        return total;
    }

    public double PrixMarkBtc()
    {
        double total = 0;
        foreach (Jambe jambe in Jambes)
        {
            total += jambe.QuantiteSignee() * jambe.Option.Mark;
        }
        return total;
    }

    // Traitable seulement si chaque jambe a un bid ET un ask
    public bool EstTraitable()
    {
        foreach (Jambe jambe in Jambes)
        {
            if (jambe.Option.Bid <= 0 || jambe.Option.Ask <= 0)
            {
                return false;
            }
        }
        return true;
    }

    // À l'achat : on paie l'ask des jambes achetées, on reçoit le bid des jambes vendues
    public double PrixExecutableAchat()
    {
        double total = 0;
        foreach (Jambe jambe in Jambes)
        {
            if (jambe.QuantiteSignee() > 0)
            {
                total += jambe.QuantiteSignee() * jambe.Option.Ask;
            }
            else
            {
                total += jambe.QuantiteSignee() * jambe.Option.Bid;
            }
        }
        return total;
    }

    // À la vente : c'est l'inverse
    public double PrixExecutableVente()
    {
        double total = 0;
        foreach (Jambe jambe in Jambes)
        {
            if (jambe.QuantiteSignee() > 0)
            {
                total += jambe.QuantiteSignee() * jambe.Option.Bid;
            }
            else
            {
                total += jambe.QuantiteSignee() * jambe.Option.Ask;
            }
        }
        return total;
    }

    public double Fourchette()
    {
        return PrixExecutableAchat() - PrixExecutableVente();
    }

    // ---- Grecques (somme pondérée par les quantités signées, annexe B) ----

    public double Delta()
    {
        double total = 0;
        foreach (Jambe jambe in Jambes) { total += jambe.QuantiteSignee() * jambe.Delta(); }
        return total;
    }

    public double Gamma()
    {
        double total = 0;
        foreach (Jambe jambe in Jambes) { total += jambe.QuantiteSignee() * jambe.Gamma(); }
        return total;
    }

    public double Vega()
    {
        double total = 0;
        foreach (Jambe jambe in Jambes) { total += jambe.QuantiteSignee() * jambe.Vega(); }
        return total;
    }

    public double Theta()
    {
        double total = 0;
        foreach (Jambe jambe in Jambes) { total += jambe.QuantiteSignee() * jambe.Theta(); }
        return total;
    }

    // Les mêmes sommes avec les grecques publiées par Deribit, pour comparer

    public double DeltaDeribit()
    {
        double total = 0;
        foreach (Jambe jambe in Jambes) { total += jambe.QuantiteSignee() * jambe.Option.DeltaDeribit; }
        return total;
    }

    public double GammaDeribit()
    {
        double total = 0;
        foreach (Jambe jambe in Jambes) { total += jambe.QuantiteSignee() * jambe.Option.GammaDeribit; }
        return total;
    }

    public double VegaDeribit()
    {
        double total = 0;
        foreach (Jambe jambe in Jambes) { total += jambe.QuantiteSignee() * jambe.Option.VegaDeribit; }
        return total;
    }

    public double ThetaDeribit()
    {
        double total = 0;
        foreach (Jambe jambe in Jambes) { total += jambe.QuantiteSignee() * jambe.Option.ThetaDeribit; }
        return total;
    }

    public void Afficher()
    {
        Console.WriteLine($"Structure : {Nom}");
        foreach (Jambe jambe in Jambes)
        {
            jambe.Afficher();
        }
    }
}
