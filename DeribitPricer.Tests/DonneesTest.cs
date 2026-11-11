using DeribitPricer.Metier;

namespace DeribitPricer.Tests;

// Petit marché fictif utilisé par les tests : pas d'appel à Deribit,
// on connaît donc à l'avance les bonnes réponses.
public static class DonneesTest
{
    // Échéance lointaine pour que les options ne soient jamais échues pendant les tests
    public static DateTime Echeance = new DateTime(2030, 12, 27, 8, 0, 0, DateTimeKind.Utc);

    public static Option CreerOption(TypeOption type, double strike, double bid, double ask)
    {
        string lettre = "C";
        if (type == TypeOption.Put)
        {
            lettre = "P";
        }

        Option option = new Option($"BTC-27DEC30-{strike}-{lettre}", type, strike, Echeance, 0.1);
        option.Bid = bid;
        option.Ask = ask;
        option.Mark = (bid + ask) / 2;
        option.MarkIv = 50;
        option.Future = 70000;
        option.Horodatage = Echeance.AddDays(-30);
        return option;
    }

    // Calls et puts de strikes 65 000, 70 000, 75 000 et 80 000
    public static List<Option> Marche()
    {
        List<Option> options = new List<Option>();

        options.Add(CreerOption(TypeOption.Call, 65000, 0.09, 0.10));
        options.Add(CreerOption(TypeOption.Call, 70000, 0.05, 0.06));
        options.Add(CreerOption(TypeOption.Call, 75000, 0.02, 0.03));
        options.Add(CreerOption(TypeOption.Call, 80000, 0.01, 0.015));

        options.Add(CreerOption(TypeOption.Put, 65000, 0.01, 0.02));
        options.Add(CreerOption(TypeOption.Put, 70000, 0.03, 0.04));
        options.Add(CreerOption(TypeOption.Put, 75000, 0.06, 0.07));
        options.Add(CreerOption(TypeOption.Put, 80000, 0.10, 0.11));

        return options;
    }

    // Call spread 70 000 / 75 000 acheté, quantité donnée
    public static Structure CallSpread(List<Option> marche, double quantite)
    {
        List<double> strikes = new List<double> { 70000, 75000 };
        return FactoryStructure.Creer(TypeStructure.CallSpread, marche, Echeance, strikes, Sens.Achat, quantite);
    }
}
