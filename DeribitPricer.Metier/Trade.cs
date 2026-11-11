namespace DeribitPricer.Metier;

// Une jambe d'un trade : ce qui a été réellement exécuté
public class LigneTrade
{
    public Option Option { get; set; }
    public double QuantiteSignee { get; set; }   // + achat, - vente
    public double PrixExecution { get; set; }    // en BTC
    public double FutureAuTrade { get; set; }    // en USD
    public double VolAuTrade { get; set; }       // vol du mark au moment du trade, en %

    public LigneTrade(Option option, double quantiteSignee, double prixExecution, double futureAuTrade, double volAuTrade)
    {
        Option = option;
        QuantiteSignee = quantiteSignee;
        PrixExecution = prixExecution;
        FutureAuTrade = futureAuTrade;
        VolAuTrade = volAuTrade;
    }
}

// Un trade du blotter : une structure traitée (ou le débouclage d'une structure)
public class Trade
{
    public int Id { get; set; }
    public DateTime Horodatage { get; set; }
    public string NomStructure { get; set; }
    public Sens Sens { get; set; }
    public double Quantite { get; set; }             // nombre de structures traitées
    public double QuantiteRestante { get; set; }     // ce qui n'a pas encore été débouclé
    public int IdOrigine { get; set; }               // 0 = trade d'origine ; sinon Id du trade débouclé
    public List<LigneTrade> Lignes { get; set; }

    public Trade(int id, DateTime horodatage, string nomStructure, Sens sens, double quantite, int idOrigine)
    {
        Id = id;
        Horodatage = horodatage;
        NomStructure = nomStructure;
        Sens = sens;
        Quantite = quantite;
        QuantiteRestante = quantite;
        IdOrigine = idOrigine;
        Lignes = new List<LigneTrade>();
    }

    // Prime totale en BTC : positive = payée, négative = reçue
    public double PrimeBtc()
    {
        double total = 0;
        foreach (LigneTrade ligne in Lignes)
        {
            total += ligne.QuantiteSignee * ligne.PrixExecution;
        }
        return total;
    }

    public void Afficher()
    {
        string lien = "";
        if (IdOrigine != 0)
        {
            lien = $"  (débouclage du trade #{IdOrigine})";
        }

        Console.WriteLine($"#{Id}  {Horodatage:dd/MM/yyyy HH:mm:ss}  {Sens} {Quantite} x {NomStructure}  Prime {PrimeBtc():F4} BTC{lien}");
        foreach (LigneTrade ligne in Lignes)
        {
            Console.WriteLine($"     {ligne.Option.Nom,-24} qté {ligne.QuantiteSignee,6}  prix {ligne.PrixExecution:F4} BTC  future {ligne.FutureAuTrade:F2}  vol {ligne.VolAuTrade:F2} %");
        }
    }
}
