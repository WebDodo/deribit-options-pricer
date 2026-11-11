using DeribitPricer.Metier;

namespace DeribitPricer.Tests;

[TestClass]
public class BookTests
{
    // ----- Blotter -----

    [TestMethod]
    public void Booker_CallSpread_PrimeEgaleAuPrixExecutable()
    {
        // Arrange
        Blotter blotter = new Blotter();
        Structure structure = DonneesTest.CallSpread(DonneesTest.Marche(), 2);

        // Act
        Trade trade = blotter.Booker(structure, Sens.Achat, 2);

        // Assert
        Assert.AreEqual(1, trade.Id);
        Assert.AreEqual(structure.PrixExecutableAchat(), trade.PrimeBtc(), 1e-9);
        Assert.AreEqual(1, blotter.Trades.Count);
    }

    [TestMethod]
    public void Deboucler_Partiel_CreeUnTradeLieEtReduitLeReste()
    {
        // Arrange : 2 call spreads achetés
        Blotter blotter = new Blotter();
        Trade origine = blotter.Booker(DonneesTest.CallSpread(DonneesTest.Marche(), 2), Sens.Achat, 2);

        // Act : on en déboucle 1
        Trade debouclage = blotter.Deboucler(origine.Id, 1);

        // Assert
        Assert.AreEqual(origine.Id, debouclage.IdOrigine);
        Assert.AreEqual(Sens.Vente, debouclage.Sens);
        Assert.AreEqual(1, origine.QuantiteRestante, 1e-9);
        // Revente du call 70 000 au bid (0,05), rachat du call 75 000 à l'ask (0,03) : on reçoit 0,02
        Assert.AreEqual(-0.02, debouclage.PrimeBtc(), 1e-9);
    }

    [TestMethod]
    public void Deboucler_QuantiteTropGrande_LeveUneException()
    {
        Blotter blotter = new Blotter();
        Trade origine = blotter.Booker(DonneesTest.CallSpread(DonneesTest.Marche(), 1), Sens.Achat, 1);

        Assert.ThrowsExactly<Exception>(() => blotter.Deboucler(origine.Id, 2));
    }

    // ----- Book -----

    [TestMethod]
    public void PnlBtc_AllerRetourImmediat_PerteEgaleALaFourchette()
    {
        // Arrange : on achète puis on revend tout de suite, sans que le marché bouge
        Blotter blotter = new Blotter();
        Book book = new Book(blotter);
        Structure structure = DonneesTest.CallSpread(DonneesTest.Marche(), 1);
        Trade origine = blotter.Booker(structure, Sens.Achat, 1);
        blotter.Deboucler(origine.Id, 1);

        // Act
        double pnl = book.PnlBtc(origine, false);

        // Assert : on a perdu exactement la fourchette, et il ne reste plus rien
        Assert.AreEqual(-structure.Fourchette(), pnl, 1e-9);
        Assert.AreEqual(0, book.ValeurBtc(origine, false), 1e-9);
    }

    [TestMethod]
    public void PnlBtc_PositionOuverte_ValeurAuMarkMoinsPrime()
    {
        // Arrange
        Blotter blotter = new Blotter();
        Book book = new Book(blotter);
        Trade origine = blotter.Booker(DonneesTest.CallSpread(DonneesTest.Marche(), 1), Sens.Achat, 1);

        // Act
        double pnl = book.PnlBtc(origine, false);

        // Assert : marks 0,055 et 0,025 → valeur 0,03 ; prime payée 0,04
        Assert.AreEqual(0.03 - 0.04, pnl, 1e-9);
    }

    // ----- Profil de P&L -----

    [TestMethod]
    public void ProfilPnL_CallSpreadAchete_GainPerteEtPointMortConnus()
    {
        // Arrange : call spread 70 000 / 75 000, prime de 1 000 USD
        Structure structure = DonneesTest.CallSpread(DonneesTest.Marche(), 1);
        double primeUsd = 1000;

        // Act
        double gainMax = ProfilPnL.GainMax(structure, primeUsd);
        double perteMax = ProfilPnL.PerteMax(structure, primeUsd);
        List<double> pointsMorts = ProfilPnL.PointsMorts(structure, primeUsd);

        // Assert
        Assert.AreEqual(5000 - 1000, gainMax, 0.01);       // largeur du spread - prime
        Assert.AreEqual(-1000, perteMax, 0.01);             // la prime
        Assert.AreEqual(1, pointsMorts.Count);
        Assert.AreEqual(71000, pointsMorts[0], 0.01);       // 70 000 + 1 000
        Assert.IsFalse(ProfilPnL.GainIllimite(structure, primeUsd));
    }
}
