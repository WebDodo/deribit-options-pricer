using DeribitPricer.Metier;

namespace DeribitPricer.Tests;

[TestClass]
public class StructureTests
{
    // ----- Factory : composition correcte -----

    [TestMethod]
    public void Creer_CallSpreadAchete_AchatK1EtVenteK2()
    {
        // Arrange
        List<Option> marche = DonneesTest.Marche();

        // Act
        Structure structure = DonneesTest.CallSpread(marche, 1);

        // Assert
        Assert.AreEqual(2, structure.Jambes.Count);
        Assert.AreEqual(70000, structure.Jambes[0].Option.Strike);
        Assert.AreEqual(Sens.Achat, structure.Jambes[0].Sens);
        Assert.AreEqual(75000, structure.Jambes[1].Option.Strike);
        Assert.AreEqual(Sens.Vente, structure.Jambes[1].Sens);
    }

    [TestMethod]
    public void Creer_ButterflyVendu_JambeCentraleAcheteeEnDouble()
    {
        // Arrange
        List<Option> marche = DonneesTest.Marche();
        List<double> strikes = new List<double> { 65000, 70000, 75000 };

        // Act
        Structure structure = FactoryStructure.Creer(TypeStructure.ButterflyCall, marche, DonneesTest.Echeance, strikes, Sens.Vente, 1);

        // Assert : vente 1 / achat 2 / vente 1
        Assert.AreEqual(-1, structure.Jambes[0].QuantiteSignee(), 1e-9);
        Assert.AreEqual(2, structure.Jambes[1].QuantiteSignee(), 1e-9);
        Assert.AreEqual(-1, structure.Jambes[2].QuantiteSignee(), 1e-9);
    }

    // ----- Factory : saisies refusées avec un message -----

    [TestMethod]
    public void Creer_StrikesALEnvers_LeveUneException()
    {
        List<Option> marche = DonneesTest.Marche();
        List<double> strikes = new List<double> { 75000, 70000 };

        Assert.ThrowsExactly<Exception>(() =>
            FactoryStructure.Creer(TypeStructure.CallSpread, marche, DonneesTest.Echeance, strikes, Sens.Achat, 1));
    }

    [TestMethod]
    public void Creer_StrikeNonCote_LeveUneException()
    {
        List<Option> marche = DonneesTest.Marche();
        List<double> strikes = new List<double> { 70000, 72500 };

        Assert.ThrowsExactly<Exception>(() =>
            FactoryStructure.Creer(TypeStructure.CallSpread, marche, DonneesTest.Echeance, strikes, Sens.Achat, 1));
    }

    [TestMethod]
    public void Creer_ButterflyNonSymetrique_LeveUneException()
    {
        List<Option> marche = DonneesTest.Marche();
        List<double> strikes = new List<double> { 65000, 70000, 80000 };

        Assert.ThrowsExactly<Exception>(() =>
            FactoryStructure.Creer(TypeStructure.ButterflyCall, marche, DonneesTest.Echeance, strikes, Sens.Achat, 1));
    }

    [TestMethod]
    public void Creer_QuantiteHorsPas_LeveUneException()
    {
        List<Option> marche = DonneesTest.Marche();

        // Le pas de cotation est 0,1 : 0,25 est refusé
        Assert.ThrowsExactly<Exception>(() => DonneesTest.CallSpread(marche, 0.25));
    }

    // ----- Prix exécutable et fourchette -----

    [TestMethod]
    public void PrixExecutable_CallSpread_PaieAskEtRecoitBid()
    {
        // Arrange : call 70 000 bid 0,05 / ask 0,06 ; call 75 000 bid 0,02 / ask 0,03
        Structure structure = DonneesTest.CallSpread(DonneesTest.Marche(), 1);

        // Act
        double achat = structure.PrixExecutableAchat();
        double vente = structure.PrixExecutableVente();

        // Assert
        Assert.AreEqual(0.06 - 0.02, achat, 1e-9);                 // on paie l'ask 70 000, on reçoit le bid 75 000
        Assert.AreEqual(0.05 - 0.03, vente, 1e-9);                 // et inversement à la vente
        Assert.AreEqual(achat - vente, structure.Fourchette(), 1e-9);
    }

    [TestMethod]
    public void EstTraitable_JambeSansBid_RenvoieFaux()
    {
        // Arrange
        List<Option> marche = DonneesTest.Marche();
        Structure structure = DonneesTest.CallSpread(marche, 1);

        // Act : plus personne n'achète le call 75 000
        structure.Jambes[1].Option.Bid = 0;

        // Assert
        Assert.IsFalse(structure.EstTraitable());
    }
}
