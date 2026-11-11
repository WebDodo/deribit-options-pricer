using System.Globalization;
using CsvHelper;
using Excel = Microsoft.Office.Interop.Excel;

namespace DeribitPricer.Metier;

// ===== Lignes exportées en CSV : une propriété = une colonne (comme dans le cours) =====

public class LigneBlotterCsv
{
    public int TradeId { get; set; }
    public string Horodatage { get; set; } = "";
    public string Structure { get; set; } = "";
    public string Sens { get; set; } = "";
    public double Quantite { get; set; }
    public int TradeOrigine { get; set; }
    public string Instrument { get; set; } = "";
    public double QuantiteSignee { get; set; }
    public double PrixExecutionBtc { get; set; }
    public double FutureAuTrade { get; set; }
    public double VolAuTrade { get; set; }
    public double PrimeTradeBtc { get; set; }
}

public class LigneBookCsv
{
    public int TradeId { get; set; }
    public string Structure { get; set; } = "";
    public string Sens { get; set; } = "";
    public double QuantiteRestante { get; set; }
    public double ValeurMarkBtc { get; set; }
    public double ValeurTheoBtc { get; set; }
    public double PnlBtc { get; set; }
    public double PnlUsd { get; set; }
    public double Delta { get; set; }
    public double Gamma { get; set; }
    public double Vega { get; set; }
    public double Theta { get; set; }
}

// ===== Partie 3.3 : exports du blotter et du book =====
public static class Export
{
    // ---------- Préparation des données ----------

    // Une ligne par jambe de chaque trade
    public static List<LigneBlotterCsv> LignesBlotter(Blotter blotter)
    {
        List<LigneBlotterCsv> lignes = new List<LigneBlotterCsv>();
        foreach (Trade trade in blotter.Trades)
        {
            foreach (LigneTrade jambe in trade.Lignes)
            {
                LigneBlotterCsv ligne = new LigneBlotterCsv();
                ligne.TradeId = trade.Id;
                ligne.Horodatage = trade.Horodatage.ToString("dd/MM/yyyy HH:mm:ss");
                ligne.Structure = trade.NomStructure;
                ligne.Sens = trade.Sens.ToString();
                ligne.Quantite = trade.Quantite;
                ligne.TradeOrigine = trade.IdOrigine;
                ligne.Instrument = jambe.Option.Nom;
                ligne.QuantiteSignee = jambe.QuantiteSignee;
                ligne.PrixExecutionBtc = jambe.PrixExecution;
                ligne.FutureAuTrade = jambe.FutureAuTrade;
                ligne.VolAuTrade = jambe.VolAuTrade;
                ligne.PrimeTradeBtc = trade.PrimeBtc();
                lignes.Add(ligne);
            }
        }
        return lignes;
    }

    // Une ligne par structure du book
    public static List<LigneBookCsv> LignesBook(Blotter blotter, Book book, double indice)
    {
        List<LigneBookCsv> lignes = new List<LigneBookCsv>();
        foreach (Trade origine in blotter.TradesOrigine())
        {
            double[] grecques = book.GrecquesStructure(origine);
            double pnl = book.PnlBtc(origine, false);

            LigneBookCsv ligne = new LigneBookCsv();
            ligne.TradeId = origine.Id;
            ligne.Structure = origine.NomStructure;
            ligne.Sens = origine.Sens.ToString();
            ligne.QuantiteRestante = origine.QuantiteRestante;
            ligne.ValeurMarkBtc = book.ValeurBtc(origine, false);
            ligne.ValeurTheoBtc = book.ValeurBtc(origine, true);
            ligne.PnlBtc = pnl;
            ligne.PnlUsd = pnl * indice;
            ligne.Delta = grecques[0];
            ligne.Gamma = grecques[1];
            ligne.Vega = grecques[2];
            ligne.Theta = grecques[3];
            lignes.Add(ligne);
        }
        return lignes;
    }

    // ---------- CSV avec CsvHelper ----------

    // Culture française : séparateur ";" et virgule décimale, pour que le CSV s'ouvre en colonnes dans Excel FR
    public static void ExporterCsv(Blotter blotter, Book book, double indice, string dossier)
    {
        CultureInfo francais = CultureInfo.GetCultureInfo("fr-FR");

        using (StreamWriter writer = new StreamWriter(Path.Combine(dossier, "blotter.csv")))
        using (CsvWriter csv = new CsvWriter(writer, francais))
        {
            csv.WriteRecords(LignesBlotter(blotter));
        }

        using (StreamWriter writer = new StreamWriter(Path.Combine(dossier, "book.csv")))
        using (CsvWriter csv = new CsvWriter(writer, francais))
        {
            csv.WriteRecords(LignesBook(blotter, book, indice));
        }
    }

    // ---------- Excel avec COM Interop ----------

    // Palette « terminal de trading » : bleu nuit + orange Bitcoin
    private static int Nuit = Couleur(14, 26, 43);
    private static int Ardoise = Couleur(36, 52, 77);
    private static int Orange = Couleur(247, 147, 26);
    private static int Gris = Couleur(107, 114, 128);
    private static int GrisClair = Couleur(244, 246, 250);
    private static int Trait = Couleur(220, 224, 232);
    private static int Vert = Couleur(30, 142, 90);
    private static int Rouge = Couleur(200, 64, 47);
    private static int Blanc = Couleur(255, 255, 255);

    // Crée le classeur, l'enregistre dans le dossier et le laisse ouvert à l'écran.
    // Renvoie le chemin du fichier.
    public static string ExporterExcel(Blotter blotter, Book book, double indice, string dossier)
    {
        Excel.Application excelApp = new Excel.Application();
        excelApp.Visible = true;
        excelApp.ScreenUpdating = false;     // on construit sans clignotement
        excelApp.DisplayAlerts = false;

        Excel.Workbook workbook = excelApp.Workbooks.Add(Type.Missing);
        string sousTitre = $"Export du {DateTime.Now:dd/MM/yyyy HH:mm:ss}   ·   Indice BTC {indice:N2} USD";

        // Feuille 1 : synthèse en tuiles
        Excel.Worksheet synthese = (Excel.Worksheet)workbook.Sheets[1];
        RemplirSynthese(synthese, blotter, book, indice, sousTitre);

        // Feuille 2 : positions et grecques du book
        Excel.Worksheet feuilleBook = (Excel.Worksheet)workbook.Sheets.Add(After: synthese);
        RemplirBook(feuilleBook, blotter, book, indice, sousTitre);

        // Feuille 3 : blotter
        Excel.Worksheet feuilleBlotter = (Excel.Worksheet)workbook.Sheets.Add(After: feuilleBook);
        RemplirBlotter(feuilleBlotter, blotter, sousTitre);

        // Si Excel a créé des feuilles vides en plus, on les supprime
        while (workbook.Sheets.Count > 3)
        {
            ((Excel.Worksheet)workbook.Sheets[workbook.Sheets.Count]).Delete();
        }

        // Sans quadrillage, on revient sur la synthèse
        CacherQuadrillage(excelApp, feuilleBlotter);
        CacherQuadrillage(excelApp, feuilleBook);
        CacherQuadrillage(excelApp, synthese);

        excelApp.ScreenUpdating = true;

        string chemin = Path.Combine(dossier, $"Book_Deribit_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        workbook.SaveAs(chemin);
        return chemin;
    }

    private static void RemplirSynthese(Excel.Worksheet ws, Blotter blotter, Book book, double indice, string sousTitre)
    {
        ws.Name = "Synthèse";
        ws.Tab.Color = Orange;
        Bandeau(ws, "BOOK OPTIONS BTC · DERIBIT", sousTitre, 12);

        double pnl = book.PnlTotalBtc(false);
        double[] grecques = book.Grecques(null);

        // Largeurs : une colonne vide entre chaque tuile
        for (int c = 1; c <= 12; c++)
        {
            ((Excel.Range)ws.Columns[c]).ColumnWidth = 15;
        }
        ((Excel.Range)ws.Columns[1]).ColumnWidth = 3;
        ((Excel.Range)ws.Columns[4]).ColumnWidth = 3;
        ((Excel.Range)ws.Columns[7]).ColumnWidth = 3;
        ((Excel.Range)ws.Columns[10]).ColumnWidth = 3;

        // Première rangée : argent
        Tuile(ws, 5, 2, "P&L TOTAL (USD)", pnl * indice, "#,##0.00", CouleurSigne(pnl));
        Tuile(ws, 5, 5, "P&L TOTAL (BTC)", pnl, "0.0000", CouleurSigne(pnl));
        Tuile(ws, 5, 8, "VALEUR AU MARK (BTC)", book.ValeurTotaleBtc(false), "0.0000", Nuit);
        Tuile(ws, 5, 11, "VALEUR THÉORIQUE (BTC)", book.ValeurTotaleBtc(true), "0.0000", Nuit);

        // Deuxième rangée : risque
        Tuile(ws, 9, 2, "DELTA", grecques[0], "0.0000", Nuit);
        Tuile(ws, 9, 5, "GAMMA", grecques[1], "0.000000", Nuit);
        Tuile(ws, 9, 8, "VEGA (USD / PT)", grecques[2], "#,##0.00", Nuit);
        Tuile(ws, 9, 11, "THETA (USD / JOUR)", grecques[3], "#,##0.00", CouleurSigne(grecques[3]));

        // Petite ligne d'activité
        Excel.Range info = Plage(ws, 13, 2, 13, 12);
        info.Merge();
        info.Value2 = $"{blotter.Trades.Count} trade(s) au blotter   ·   {blotter.TradesOrigine().Count} structure(s)   ·   détails dans les onglets Book et Blotter";
        info.Font.Color = Gris;
        info.Font.Italic = true;
    }

    private static void RemplirBook(Excel.Worksheet ws, Blotter blotter, Book book, double indice, string sousTitre)
    {
        ws.Name = "Book";
        ws.Tab.Color = Ardoise;
        Bandeau(ws, "POSITIONS DU BOOK", sousTitre, 12);

        // Tableau 1 : une ligne par structure + total
        List<LigneBookCsv> lignes = LignesBook(blotter, book, indice);
        object[,] donnees = new object[lignes.Count + 2, 12];
        string[] entetes = { "Trade", "Structure", "Sens", "Reste", "Valeur mark (BTC)", "Valeur théo (BTC)",
                             "P&L (BTC)", "P&L (USD)", "Delta", "Gamma", "Vega", "Theta" };
        for (int c = 0; c < 12; c++)
        {
            donnees[0, c] = entetes[c];
        }

        for (int i = 0; i < lignes.Count; i++)
        {
            LigneBookCsv l = lignes[i];
            donnees[i + 1, 0] = "#" + l.TradeId;
            donnees[i + 1, 1] = l.Structure;
            donnees[i + 1, 2] = l.Sens;
            donnees[i + 1, 3] = l.QuantiteRestante;
            donnees[i + 1, 4] = l.ValeurMarkBtc;
            donnees[i + 1, 5] = l.ValeurTheoBtc;
            donnees[i + 1, 6] = l.PnlBtc;
            donnees[i + 1, 7] = l.PnlUsd;
            donnees[i + 1, 8] = l.Delta;
            donnees[i + 1, 9] = l.Gamma;
            donnees[i + 1, 10] = l.Vega;
            donnees[i + 1, 11] = l.Theta;
        }

        double[] total = book.Grecques(null);
        double pnlTotal = book.PnlTotalBtc(false);
        int derniere = lignes.Count + 1;
        donnees[derniere, 0] = "TOTAL";
        donnees[derniere, 4] = book.ValeurTotaleBtc(false);
        donnees[derniere, 5] = book.ValeurTotaleBtc(true);
        donnees[derniere, 6] = pnlTotal;
        donnees[derniere, 7] = pnlTotal * indice;
        donnees[derniere, 8] = total[0];
        donnees[derniere, 9] = total[1];
        donnees[derniere, 10] = total[2];
        donnees[derniere, 11] = total[3];

        string[] formats = { "@", "@", "@", "0.0", "0.0000", "0.0000", "0.0000", "#,##0.00",
                             "0.0000", "0.000000", "#,##0.00", "#,##0.00" };

        Section(ws, 5, "Structures");
        int fin = Tableau(ws, 6, donnees, formats);
        LigneTotal(ws, fin, 12);
        ColorerSigne(ws, 7, fin, 7);
        ColorerSigne(ws, 7, fin, 8);

        // Tableau 2 : grecques par échéance
        List<DateTime> echeances = book.EcheancesOuvertes();
        object[,] grecques = new object[echeances.Count + 2, 5];
        string[] entetesGrecques = { "Échéance", "Delta", "Gamma", "Vega", "Theta" };
        for (int c = 0; c < 5; c++)
        {
            grecques[0, c] = entetesGrecques[c];
        }
        for (int i = 0; i < echeances.Count; i++)
        {
            double[] g = book.Grecques(echeances[i]);
            grecques[i + 1, 0] = echeances[i].ToString("dd/MM/yyyy");
            grecques[i + 1, 1] = g[0];
            grecques[i + 1, 2] = g[1];
            grecques[i + 1, 3] = g[2];
            grecques[i + 1, 4] = g[3];
        }
        grecques[echeances.Count + 1, 0] = "TOTAL";
        grecques[echeances.Count + 1, 1] = total[0];
        grecques[echeances.Count + 1, 2] = total[1];
        grecques[echeances.Count + 1, 3] = total[2];
        grecques[echeances.Count + 1, 4] = total[3];

        string[] formatsGrecques = { "@", "0.0000", "0.000000", "#,##0.00", "#,##0.00" };

        int debutGrecques = fin + 3;
        Section(ws, debutGrecques, "Grecques par échéance");
        int finGrecques = Tableau(ws, debutGrecques + 1, grecques, formatsGrecques);
        LigneTotal(ws, finGrecques, 5);

        ws.Columns.AutoFit();
        ((Excel.Range)ws.Columns[2]).ColumnWidth = 34;
    }

    private static void RemplirBlotter(Excel.Worksheet ws, Blotter blotter, string sousTitre)
    {
        ws.Name = "Blotter";
        ws.Tab.Color = Gris;
        Bandeau(ws, "BLOTTER · HISTORIQUE DES TRADES", sousTitre, 12);

        List<LigneBlotterCsv> lignes = LignesBlotter(blotter);
        object[,] donnees = new object[lignes.Count + 1, 12];
        string[] entetes = { "Trade", "Horodatage", "Structure", "Sens", "Qté", "Trade d'origine",
                             "Instrument", "Qté signée", "Prix exéc. (BTC)", "Future (USD)", "Vol (%)", "Prime trade (BTC)" };
        for (int c = 0; c < 12; c++)
        {
            donnees[0, c] = entetes[c];
        }

        for (int i = 0; i < lignes.Count; i++)
        {
            LigneBlotterCsv l = lignes[i];
            donnees[i + 1, 0] = "#" + l.TradeId;
            donnees[i + 1, 1] = l.Horodatage;
            donnees[i + 1, 2] = l.Structure;
            donnees[i + 1, 3] = l.Sens;
            donnees[i + 1, 4] = l.Quantite;
            if (l.TradeOrigine == 0)
            {
                donnees[i + 1, 5] = "—";
            }
            else
            {
                donnees[i + 1, 5] = "#" + l.TradeOrigine;
            }
            donnees[i + 1, 6] = l.Instrument;
            donnees[i + 1, 7] = l.QuantiteSignee;
            donnees[i + 1, 8] = l.PrixExecutionBtc;
            donnees[i + 1, 9] = l.FutureAuTrade;
            donnees[i + 1, 10] = l.VolAuTrade;
            donnees[i + 1, 11] = l.PrimeTradeBtc;
        }

        string[] formats = { "@", "@", "@", "@", "0.0", "@", "@", "0.0", "0.0000", "#,##0.00", "0.00", "0.0000" };

        int fin = Tableau(ws, 5, donnees, formats);
        ColorerSigne(ws, 6, fin, 8);
        ws.Columns.AutoFit();
    }

    // ---------- Petites briques de mise en forme ----------

    // Excel attend les couleurs au format BGR : rouge + vert x 256 + bleu x 65536
    private static int Couleur(int rouge, int vert, int bleu)
    {
        return rouge + vert * 256 + bleu * 65536;
    }

    private static Excel.Range Plage(Excel.Worksheet ws, int ligne1, int colonne1, int ligne2, int colonne2)
    {
        Excel.Range debut = (Excel.Range)ws.Cells[ligne1, colonne1];
        Excel.Range fin = (Excel.Range)ws.Cells[ligne2, colonne2];
        return ws.Range[debut, fin];
    }

    // Bandeau bleu nuit avec titre, sous-titre orange et fin trait orange
    private static void Bandeau(Excel.Worksheet ws, string titre, string sousTitre, int nbColonnes)
    {
        ws.Cells.Font.Name = "Segoe UI";
        ws.Cells.Font.Size = 10;

        Excel.Range bandeau = Plage(ws, 1, 1, 2, nbColonnes);
        bandeau.Interior.Color = Nuit;

        Excel.Range ligneTitre = Plage(ws, 1, 1, 1, nbColonnes);
        ligneTitre.Merge();
        ligneTitre.Value2 = "  " + titre;
        ligneTitre.Font.Size = 18;
        ligneTitre.Font.Bold = true;
        ligneTitre.Font.Color = Blanc;
        ligneTitre.RowHeight = 36;
        ligneTitre.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;

        Excel.Range ligneSousTitre = Plage(ws, 2, 1, 2, nbColonnes);
        ligneSousTitre.Merge();
        ligneSousTitre.Value2 = "  " + sousTitre;
        ligneSousTitre.Font.Color = Orange;
        ligneSousTitre.RowHeight = 20;

        Excel.Range trait = Plage(ws, 3, 1, 3, nbColonnes);
        trait.Interior.Color = Orange;
        trait.RowHeight = 4;
    }

    private static void Section(Excel.Worksheet ws, int ligne, string texte)
    {
        Excel.Range cellule = (Excel.Range)ws.Cells[ligne, 1];
        cellule.Value2 = texte.ToUpper();
        cellule.Font.Bold = true;
        cellule.Font.Size = 11;
        cellule.Font.Color = Orange;
    }

    // Tuile de synthèse : petit libellé gris, grand chiffre, bord gauche orange
    private static void Tuile(Excel.Worksheet ws, int ligne, int colonne, string libelle, double valeur, string format, int couleurValeur)
    {
        Excel.Range fond = Plage(ws, ligne, colonne, ligne + 2, colonne + 1);
        fond.Interior.Color = GrisClair;
        fond.Borders[Excel.XlBordersIndex.xlEdgeLeft].LineStyle = Excel.XlLineStyle.xlContinuous;
        fond.Borders[Excel.XlBordersIndex.xlEdgeLeft].Weight = Excel.XlBorderWeight.xlThick;
        fond.Borders[Excel.XlBordersIndex.xlEdgeLeft].Color = Orange;

        Excel.Range titre = Plage(ws, ligne, colonne, ligne, colonne + 1);
        titre.Merge();
        titre.Value2 = "  " + libelle;
        titre.Font.Size = 8;
        titre.Font.Bold = true;
        titre.Font.Color = Gris;
        titre.RowHeight = 20;
        titre.VerticalAlignment = Excel.XlVAlign.xlVAlignBottom;

        Excel.Range chiffre = Plage(ws, ligne + 1, colonne, ligne + 1, colonne + 1);
        chiffre.Merge();
        chiffre.Value2 = valeur;
        chiffre.NumberFormat = format;
        chiffre.Font.Size = 20;
        chiffre.Font.Bold = true;
        chiffre.Font.Color = couleurValeur;
        chiffre.RowHeight = 32;
        chiffre.HorizontalAlignment = Excel.XlHAlign.xlHAlignLeft;
        chiffre.IndentLevel = 1;

        Excel.Range marge = Plage(ws, ligne + 2, colonne, ligne + 2, colonne + 1);
        marge.RowHeight = 8;
    }

    // Écrit la matrice d'un coup avec Value2 (comme dans le cours), puis la met en forme.
    // La première ligne de la matrice est l'en-tête. Renvoie le numéro de la dernière ligne.
    private static int Tableau(Excel.Worksheet ws, int ligne, object[,] donnees, string[] formats)
    {
        int nbLignes = donnees.GetLength(0);
        int nbColonnes = donnees.GetLength(1);
        int fin = ligne + nbLignes - 1;

        // Formats des colonnes avant d'écrire ("@" = texte)
        for (int c = 0; c < nbColonnes; c++)
        {
            Plage(ws, ligne + 1, c + 1, fin, c + 1).NumberFormat = formats[c];
        }

        Excel.Range tout = Plage(ws, ligne, 1, fin, nbColonnes);
        tout.Value2 = donnees;

        // En-tête ardoise, texte blanc
        Excel.Range entete = Plage(ws, ligne, 1, ligne, nbColonnes);
        entete.Interior.Color = Ardoise;
        entete.Font.Color = Blanc;
        entete.Font.Bold = true;
        entete.RowHeight = 24;
        entete.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;

        // Lignes alternées et fin trait sous chaque ligne
        for (int l = ligne + 1; l <= fin; l++)
        {
            Excel.Range rangee = Plage(ws, l, 1, l, nbColonnes);
            rangee.RowHeight = 20;
            rangee.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
            if ((l - ligne) % 2 == 0)
            {
                rangee.Interior.Color = GrisClair;
            }
            rangee.Borders[Excel.XlBordersIndex.xlEdgeBottom].LineStyle = Excel.XlLineStyle.xlContinuous;
            rangee.Borders[Excel.XlBordersIndex.xlEdgeBottom].Color = Trait;
        }
        return fin;
    }

    // Dernière ligne d'un tableau : gras, trait orange au-dessus
    private static void LigneTotal(Excel.Worksheet ws, int ligne, int nbColonnes)
    {
        Excel.Range total = Plage(ws, ligne, 1, ligne, nbColonnes);
        total.Font.Bold = true;
        total.Interior.Color = Blanc;
        total.Borders[Excel.XlBordersIndex.xlEdgeTop].LineStyle = Excel.XlLineStyle.xlContinuous;
        total.Borders[Excel.XlBordersIndex.xlEdgeTop].Weight = Excel.XlBorderWeight.xlMedium;
        total.Borders[Excel.XlBordersIndex.xlEdgeTop].Color = Orange;
    }

    // Vert si positif, rouge si négatif
    private static int CouleurSigne(double valeur)
    {
        if (valeur < 0)
        {
            return Rouge;
        }
        return Vert;
    }

    private static void ColorerSigne(Excel.Worksheet ws, int ligneDebut, int ligneFin, int colonne)
    {
        for (int l = ligneDebut; l <= ligneFin; l++)
        {
            Excel.Range cellule = (Excel.Range)ws.Cells[l, colonne];
            if (cellule.Value2 is double valeur)
            {
                cellule.Font.Color = CouleurSigne(valeur);
            }
        }
    }

    private static void CacherQuadrillage(Excel.Application excelApp, Excel.Worksheet ws)
    {
        ((Excel._Worksheet)ws).Activate();
        excelApp.ActiveWindow.DisplayGridlines = false;
    }
}
