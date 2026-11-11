using DeribitPricer.Metier;

namespace DeribitPricer.Tests;

[TestClass]
public class Black76Tests
{
    // ----- Prix -----

    [TestMethod]
    public void PrixUsd_CallALaMonnaie_DonneLaValeurConnue()
    {
        // Arrange
        double future = 100;
        double strike = 100;
        double maturite = 1;
        double vol = 0.20;

        // Act
        double prix = Black76.PrixUsd(TypeOption.Call, future, strike, maturite, vol);

        // Assert
        Assert.AreEqual(7.965567, prix, 0.000001);
    }

    [TestMethod]
    public void PrixUsd_PariteCallPut_CallMoinsPutEgaleFMoinsK()
    {
        // Arrange
        double future = 110;
        double strike = 100;
        double maturite = 0.5;
        double vol = 0.30;

        // Act
        double call = Black76.PrixUsd(TypeOption.Call, future, strike, maturite, vol);
        double put = Black76.PrixUsd(TypeOption.Put, future, strike, maturite, vol);

        // Assert
        Assert.AreEqual(future - strike, call - put, 0.000001);
    }

    [TestMethod]
    public void PrixUsd_MaturiteNulle_DonneLePayoff()
    {
        // Act
        double call = Black76.PrixUsd(TypeOption.Call, 120, 100, 0, 0.5);
        double put = Black76.PrixUsd(TypeOption.Put, 120, 100, 0, 0.5);

        // Assert
        Assert.AreEqual(20, call, 0.000001);
        Assert.AreEqual(0, put, 0.000001);
    }

    // ----- Grecques -----

    [TestMethod]
    public void Delta_CallMoinsPut_EgaleUn()
    {
        // Act
        double deltaCall = Black76.Delta(TypeOption.Call, 105, 100, 0.25, 0.4);
        double deltaPut = Black76.Delta(TypeOption.Put, 105, 100, 0.25, 0.4);

        // Assert
        Assert.AreEqual(1, deltaCall - deltaPut, 0.000001);
    }

    // ----- Vol implicite -----

    [TestMethod]
    public void VolImplicite_PrixCalculeAvec50Pourcent_Retrouve50Pourcent()
    {
        // Arrange : on fabrique un prix avec une vol connue de 50 %
        double future = 70000;
        double strike = 75000;
        double maturite = 0.1;
        double prixBtc = Black76.PrixBtc(TypeOption.Call, future, strike, maturite, 0.50);

        // Act : on demande à VolImplicite de retrouver la vol
        double vol = Black76.VolImplicite(TypeOption.Call, future, strike, maturite, prixBtc);

        // Assert
        Assert.AreEqual(0.50, vol, 0.0001);
    }

    [TestMethod]
    public void VolImplicite_PrixNul_RenvoieMoinsUn()
    {
        // Act : un prix de 0 est sous la borne basse, aucune vol possible
        double vol = Black76.VolImplicite(TypeOption.Call, 70000, 75000, 0.1, 0);

        // Assert
        Assert.AreEqual(-1, vol);
    }

    // ----- Maturité -----

    [TestMethod]
    public void Maturite_Dans365Jours_DonneUnAn()
    {
        // Arrange
        DateTime maintenant = new DateTime(2026, 1, 1);
        DateTime echeance = maintenant.AddDays(365);

        // Act
        double maturite = Black76.Maturite(echeance, maintenant);

        // Assert
        Assert.AreEqual(1, maturite, 0.000001);
    }
}