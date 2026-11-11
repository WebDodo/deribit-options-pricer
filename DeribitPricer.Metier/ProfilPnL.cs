namespace DeribitPricer.Metier;

// Partie 2.3 : profil de P&L d'une structure (en USD)
public static class ProfilPnL
{
    // Grille fine utilisée pour chercher gain max, perte max et points morts
    private const int NbPoints = 30000;

    // Prime payée pour traiter la structure, convertie en USD au future actuel.
    // On prend le prix exécutable à l'achat ; si la structure n'est pas traitable, le prix théorique.
    public static double PrimeUsd(Structure structure)
    {
        double future = structure.Jambes[0].Option.Future;

        double primeBtc;
        if (structure.EstTraitable())
        {
            primeBtc = structure.PrixExecutableAchat();
        }
        else
        {
            primeBtc = structure.PrixTheoBtc();
        }

        return primeBtc * future;
    }

    // P&L à l'échéance si le sous-jacent finit à "sousJacent" : payoff moins prime payée
    public static double PnlEcheance(Structure structure, double sousJacent, double primeUsd)
    {
        double total = 0;
        foreach (Jambe jambe in structure.Jambes)
        {
            Option o = jambe.Option;
            // Avec une maturité de 0, PrixUsd renvoie le payoff
            total += jambe.QuantiteSignee() * Black76.PrixUsd(o.Type, sousJacent, o.Strike, 0, 0);
        }
        return total - primeUsd;
    }

    // P&L aujourd'hui : chaque jambe réévaluée avec Black-76, à vol inchangée
    public static double PnlAujourdhui(Structure structure, double sousJacent, double primeUsd)
    {
        double total = 0;
        foreach (Jambe jambe in structure.Jambes)
        {
            Option o = jambe.Option;
            total += jambe.QuantiteSignee() * Black76.PrixUsd(o.Type, sousJacent, o.Strike, jambe.Maturite(), o.MarkIv / 100);
        }
        return total - primeUsd;
    }

    // Point numéro i de la grille fine : de 1 % à 300 % du future
    private static double PointGrille(double future, int i)
    {
        double minimum = 0.01 * future;
        double maximum = 3 * future;
        return minimum + i * (maximum - minimum) / NbPoints;
    }

    public static double GainMax(Structure structure, double primeUsd)
    {
        double future = structure.Jambes[0].Option.Future;
        double max = PnlEcheance(structure, PointGrille(future, 0), primeUsd);

        for (int i = 1; i <= NbPoints; i++)
        {
            double pnl = PnlEcheance(structure, PointGrille(future, i), primeUsd);
            if (pnl > max)
            {
                max = pnl;
            }
        }
        return max;
    }

    public static double PerteMax(Structure structure, double primeUsd)
    {
        double future = structure.Jambes[0].Option.Future;
        double min = PnlEcheance(structure, PointGrille(future, 0), primeUsd);

        for (int i = 1; i <= NbPoints; i++)
        {
            double pnl = PnlEcheance(structure, PointGrille(future, i), primeUsd);
            if (pnl < min)
            {
                min = pnl;
            }
        }
        return min;
    }

    // Pente du P&L tout en haut de la grille : positive = gain illimité, négative = perte illimitée
    private static double PenteFinale(Structure structure, double primeUsd)
    {
        double future = structure.Jambes[0].Option.Future;
        double avant = PnlEcheance(structure, PointGrille(future, NbPoints - 1), primeUsd);
        double fin = PnlEcheance(structure, PointGrille(future, NbPoints), primeUsd);
        return fin - avant;
    }

    public static bool GainIllimite(Structure structure, double primeUsd)
    {
        return PenteFinale(structure, primeUsd) > 1e-6;
    }

    public static bool PerteIllimitee(Structure structure, double primeUsd)
    {
        return PenteFinale(structure, primeUsd) < -1e-6;
    }

    // Points morts à l'échéance : là où le P&L change de signe
    public static List<double> PointsMorts(Structure structure, double primeUsd)
    {
        double future = structure.Jambes[0].Option.Future;
        List<double> points = new List<double>();

        double sAvant = PointGrille(future, 0);
        double pnlAvant = PnlEcheance(structure, sAvant, primeUsd);

        for (int i = 1; i <= NbPoints; i++)
        {
            double s = PointGrille(future, i);
            double pnl = PnlEcheance(structure, s, primeUsd);

            if ((pnlAvant < 0 && pnl >= 0) || (pnlAvant > 0 && pnl <= 0))
            {
                // Interpolation linéaire entre les deux points pour trouver le zéro
                double zero = sAvant + (s - sAvant) * (-pnlAvant) / (pnl - pnlAvant);
                points.Add(zero);
            }

            sAvant = s;
            pnlAvant = pnl;
        }
        return points;
    }
}
