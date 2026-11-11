namespace DeribitPricer.Metier;

// Partie 3.1 : le blotter enregistre tous les trades (bookings et débouclages)
public class Blotter
{
    public List<Trade> Trades { get; set; }
    private int _prochainId;

    public Blotter()
    {
        Trades = new List<Trade>();
        _prochainId = 1;
    }

    // Traite une structure au prix exécutable affiché
    public Trade Booker(Structure structure, Sens sens, double quantite)
    {
        if (!structure.EstTraitable())
        {
            throw new Exception("Structure non traitable : une jambe n'a pas de bid ou pas d'ask.");
        }

        Trade trade = new Trade(_prochainId, DateTime.Now, structure.Nom, sens, quantite, 0);
        _prochainId++;

        foreach (Jambe jambe in structure.Jambes)
        {
            double quantiteSignee = jambe.QuantiteSignee();
            double prix = PrixExecution(jambe.Option, quantiteSignee);
            trade.Lignes.Add(new LigneTrade(jambe.Option, quantiteSignee, prix, jambe.Option.Future, jambe.Option.MarkIv));
        }

        Trades.Add(trade);
        return trade;
    }

    // Déboucle tout ou partie d'un trade d'origine, au prix exécutable du moment
    public Trade Deboucler(int idTrade, double quantite)
    {
        Trade origine = TrouverTrade(idTrade);

        if (origine.IdOrigine != 0)
        {
            throw new Exception("On ne peut déboucler qu'un trade d'origine, pas un débouclage.");
        }
        if (quantite <= 0 || quantite > origine.QuantiteRestante + 1e-9)
        {
            throw new Exception($"Quantité invalide : il reste {origine.QuantiteRestante} structure(s) à déboucler.");
        }

        double pas = origine.Lignes[0].Option.QuantiteMin;
        double nombreDePas = quantite / pas;
        if (Math.Abs(nombreDePas - Math.Round(nombreDePas)) > 1e-9)
        {
            throw new Exception($"La quantité {quantite} n'est pas un multiple du pas de cotation ({pas}).");
        }

        foreach (LigneTrade ligne in origine.Lignes)
        {
            if (ligne.Option.Bid <= 0 || ligne.Option.Ask <= 0)
            {
                throw new Exception($"Débouclage impossible : {ligne.Option.Nom} n'a pas de bid ou pas d'ask.");
            }
        }

        Sens sensInverse = Sens.Achat;
        if (origine.Sens == Sens.Achat)
        {
            sensInverse = Sens.Vente;
        }

        Trade debouclage = new Trade(_prochainId, DateTime.Now, origine.NomStructure, sensInverse, quantite, origine.Id);
        _prochainId++;

        // Chaque jambe est retournée, au prorata de la quantité débouclée
        double ratio = quantite / origine.Quantite;
        foreach (LigneTrade ligne in origine.Lignes)
        {
            double quantiteSignee = -ligne.QuantiteSignee * ratio;
            double prix = PrixExecution(ligne.Option, quantiteSignee);
            debouclage.Lignes.Add(new LigneTrade(ligne.Option, quantiteSignee, prix, ligne.Option.Future, ligne.Option.MarkIv));
        }

        origine.QuantiteRestante -= quantite;
        Trades.Add(debouclage);
        return debouclage;
    }

    public Trade TrouverTrade(int id)
    {
        foreach (Trade trade in Trades)
        {
            if (trade.Id == id)
            {
                return trade;
            }
        }
        throw new Exception($"Le trade #{id} n'existe pas.");
    }

    // Les débouclages liés à un trade d'origine
    public List<Trade> Debouclages(Trade origine)
    {
        List<Trade> liste = new List<Trade>();
        foreach (Trade trade in Trades)
        {
            if (trade.IdOrigine == origine.Id)
            {
                liste.Add(trade);
            }
        }
        return liste;
    }

    // Les trades d'origine (une "structure" du book chacun)
    public List<Trade> TradesOrigine()
    {
        List<Trade> liste = new List<Trade>();
        foreach (Trade trade in Trades)
        {
            if (trade.IdOrigine == 0)
            {
                liste.Add(trade);
            }
        }
        return liste;
    }

    // On paie l'ask quand on achète, on reçoit le bid quand on vend
    private double PrixExecution(Option option, double quantiteSignee)
    {
        if (quantiteSignee > 0)
        {
            return option.Ask;
        }
        return option.Bid;
    }
}
