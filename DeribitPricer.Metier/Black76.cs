using MathNet.Numerics.Distributions;

namespace DeribitPricer.Metier;

public static class Black76
{
    // Maturité en années (annexe A)
    public static double Maturite(DateTime echeance, DateTime maintenant)
    {
        double secondes = (echeance-maintenant).TotalSeconds;
        return secondes/(365*24*3600);
    }

    // d1 (annexe A)
    public static double D1(double future, double strike, double maturite, double vol)
    {
        return (Math.Log(future/strike) + vol * vol * maturite / 2 )/ (vol*Math.Sqrt(maturite));
    }

    // Fonction de répartition de la loi Gaussienne
    private static double N(double x)
    {
        return Normal.CDF(0,1,x);
    }

    // Prix en USD (annexe A)
    public static double PrixUsd(TypeOption type, double future, double strike, double maturite, double vol)
    {
        // A l'échéance: Le rpix = le payoff
        if (maturite <= 0)
        {
            if (type == TypeOption.Call)
            {
                return Math.Max(future-strike,0 );
            }
            return Math.Max(strike - future, 0);     
        }

        double d1 = D1(future, strike, maturite, vol);
        double d2 = d1- vol*Math.Sqrt(maturite);

        if (type == TypeOption.Call)
        {
            return future*N(d1) - strike*N(d2);
        }
        return strike*N(-d2) - future*N(-d1);
    }

    // Prix en BTC, comme Deribit (annexe A)
    public static double PrixBtc(TypeOption type, double future, double strike, double maturite, double vol)
    {
        return PrixUsd(type, future, strike, maturite, vol)/ future;
    }

    // Densité loi gaussienne n(x)
    private static double Densite(double x)
    {
        return Normal.PDF(0, 1, x);
    }

    // Delta (annexe B) : en unite de sous-jacent
    public static double Delta(TypeOption type, double future, double strike, double maturite, double vol)
    {
        // A l'échéance : 1 (ou -1) si l'option finit ITM, 0 sinon
        if (maturite <= 0)
        {
            if (type == TypeOption.Call)
            {
                if (future > strike){return 1;}
                return 0;
            }
            if (future < strike) {return -1; }
            return 0;
        }

        double d1 = D1(future, strike, maturite, vol);

        if (type == TypeOption.Call){ return N(d1);}
        return N(d1) - 1 ;
    }

    // Gamma (annexe B) : Identique pour call et put
    public static double Gamma(double future, double strike, double maturite, double vol)
    {
        if (maturite<=0) {return 0;}

        double d1 = D1(future, strike, maturite, vol);
        return Densite(d1)/(future*vol*Math.Sqrt(maturite));
    }

    // Vega brut (annexe B sans la division par  100) : sert à Newtone pour la vol implicite
    public static double VegaBrut(double future, double strike, double maturite, double vol)
    {
        if (maturite<=0){return 0; }
        double d1 = D1(future, strike, maturite, vol);
        return future*Densite(d1)* Math.Sqrt(maturite);
    }

    // Vega (annexe B) : en USD par point de vol
    public static double Vega(double future, double strike, double maturite,double vol)
    {
        return VegaBrut(future, strike, maturite, vol)/100;
    }

    // Theta (annexe B) : en USD par jour calendaire
    public static double Theta(double future, double strike, double maturite, double vol)
    {
        if (maturite <=0) {return 0;}
        double d1 = D1(future, strike, maturite, vol);
        return -future * Densite(d1) * vol / (2 * Math.Sqrt(maturite)) / 365;
    }

    // Vol implicite (annexe A) : la vol qui redonne le prix de marché
    // Renvoie -1 si la vol est impossible à calculer
    public static double VolImplicite(TypeOption type, double future, double strike, double maturite, double prixBtc)
    {
        if (maturite <= 0)
        {
            return -1;
        }

        // 1. Prix de marché en USD
        double prixMarche = prixBtc * future;

        // 2. Bornes de non-arbitrage
        double borneBasse;
        double borneHaute;
        if (type == TypeOption.Call)
        {
            borneBasse = Math.Max(future - strike, 0);
            borneHaute = future;
        }
        else
        {
            borneBasse = Math.Max(strike - future, 0);
            borneHaute = strike;
        }

        if (prixMarche <= borneBasse || prixMarche >= borneHaute)
        {
            return -1;
        }

        double volMin = 0.0001;   // 0,01 %
        double volMax = 5.0;      // 500 %

        // 3. Méthode de Newton, en partant de 50 %
        double vol = 0.5;
        for (int i = 0; i < 100; i++)
        {
            double ecart = PrixUsd(type, future, strike, maturite, vol) - prixMarche;

            if (Math.Abs(ecart) < 1e-6)
            {
                return vol;   // trouvé
            }

            double vega = VegaBrut(future, strike, maturite, vol);
            if (vega < 1e-6)
            {
                break;        // vega trop petit : on passe à la dichotomie
            }

            vol = vol - ecart / vega;

            if (vol < volMin || vol > volMax)
            {
                break;        // sortie de l'intervalle : on passe à la dichotomie
            }
        }

        // 4. Dichotomie entre volMin et volMax
        double bas = volMin;
        double haut = volMax;
        for (int i = 0; i < 200; i++)
        {
            double milieu = (bas + haut) / 2;
            double ecart = PrixUsd(type, future, strike, maturite, milieu) - prixMarche;

            if (Math.Abs(ecart) < 1e-6)
            {
                return milieu;   // trouvé
            }

            if (ecart > 0)
            {
                haut = milieu;   // prix trop haut : la vol est dans la moitié basse
            }
            else
            {
                bas = milieu;    // prix trop bas : la vol est dans la moitié haute
            }
        }

        return -1;   // pas trouvé dans l'intervalle
    }
}
