namespace DeribitPricer.Metier;

public enum TypeOption
{
    Call,
    Put
}

public class Option
{
    // Caractéristiques de l'option
    public string Nom {get; set; }
    public TypeOption Type {get; set; }
    public double Strike {get; set; }
    public DateTime Echeance{ get; set; }
    public double QuantiteMin { get; set;}

    // Cotation
    public double Bid {get; set; }
    public double Ask {get; set; }
    public double Mark {get; set; }
    public double MarkIv{get; set; }
    public double Future {get; set; }

    // Données du ticker Deribit (pour comparer avec nos calculs)
    public double BidIv { get; set; }          // vol du bid selon Deribit, en %
    public double AskIv { get; set; }          // vol de l'ask selon Deribit, en %
    public double DeltaDeribit { get; set; }
    public double GammaDeribit { get; set; }
    public double VegaDeribit { get; set; }
    public double ThetaDeribit { get; set; }
    public DateTime Horodatage { get; set; }   // heure de la cotation Deribit

    // Constructeur

    public Option(string nom, TypeOption type, double strike, DateTime echeance, double quantiteMin)
    {
        Nom = nom;
        Type = type;
        Strike =strike;
        Echeance = echeance;
        QuantiteMin = quantiteMin;
    }

    // Méthode
    public void Afficher()
    {
        Console.WriteLine($"{Nom,-25} Bid {Bid,7:F4} Ask {Ask,7:F4}  Mark {Mark,7:F4}  Vol {MarkIv,6:F2} %");
    }

}
