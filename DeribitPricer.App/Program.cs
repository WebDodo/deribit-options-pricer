using DeribitPricer.Metier;

// ===== Partie 4 : le mode (live, enregistrement, rejeu) vient de appsettings.json =====
ClientDeribit client;
List<Option> options;
int pause = 2000;   // rafraîchissement des écrans en continu (ms)

try
{
    Configuration config = Configuration.Charger();
    string fichier = Path.GetFullPath(config.Fichier);

    ISourceDonnees source;
    if (config.Mode == "Rejeu")
    {
        source = new SourceRejeu(fichier);
        pause = 2000 / config.Vitesse;   // rejeu accéléré : on rafraîchit plus vite
        Console.WriteLine($"Mode REJEU : lecture de {fichier} (vitesse x{config.Vitesse})");
    }
    else if (config.Mode == "Enregistrement")
    {
        source = new SourceLive(fichier);
        Console.WriteLine($"Mode LIVE avec ENREGISTREMENT dans {fichier}");
    }
    else
    {
        source = new SourceLive(null);
        Console.WriteLine("Mode LIVE");
    }

    client = new ClientDeribit(source);

    Console.WriteLine("Chargement des options BTC...");
    options = await client.ChargerOptions("BTC");
    await client.MettreAJourPrix(options, "BTC");
}
catch (Exception erreur)
{
    Console.WriteLine($"Démarrage impossible : {erreur.Message}");
    return;
}
Console.WriteLine($"{options.Count} options chargées.");

// Partie 3 : le blotter enregistre les trades, le book suit les positions
Blotter blotter = new Blotter();
Book book = new Book(blotter);

// ===== Menu principal =====
bool continuer = true;

while (continuer)
{
    Console.WriteLine();
    Console.WriteLine("===== MENU =====");
    Console.WriteLine("1. Parcourir les options");
    Console.WriteLine("2. Composer, coter et traiter une structure");
    Console.WriteLine("3. Afficher le blotter");
    Console.WriteLine("4. Déboucler un trade");
    Console.WriteLine("5. Suivi du book (en continu)");
    Console.WriteLine("6. Exporter le blotter et le book (CSV + Excel)");
    Console.WriteLine("0. Quitter");
    Console.Write("Votre choix : ");
    string? choix = Console.ReadLine();

    if (choix == "1")
    {
        await client.MettreAJourPrix(options, "BTC");   // prix frais à chaque fois
        ParcourirOptions();
    }
    else if (choix == "2")
    {
        await client.MettreAJourPrix(options, "BTC");
        await ComposerStructure();
    }
    else if (choix == "3")
    {
        AfficherBlotter();
    }
    else if (choix == "4")
    {
        await client.MettreAJourPrix(options, "BTC");   // prix du moment
        DeboucherTrade();
    }
    else if (choix == "5")
    {
        await SuiviBook();
    }
    else if (choix == "6")
    {
        await Exporter();
    }
    else if (choix == "0")
    {
        continuer = false;
    }
    else
    {
        Console.WriteLine("Choix invalide.");
    }
}

// ===== Choix d'une échéance (utilisé par les deux menus) =====   NOUVEAU
// Renvoie l'échéance choisie, ou null si la saisie est invalide
DateTime? ChoisirEcheance()
{
    List<DateTime> echeances = options.Select(o => o.Echeance).Distinct().OrderBy(d => d).ToList();

    for (int i = 0; i < echeances.Count; i++)
    {
        Console.WriteLine($"{i + 1}. {echeances[i]:dd/MM/yyyy}");
    }

    Console.Write("Numéro de l'échéance : ");
    string? saisie = Console.ReadLine();

    if (!int.TryParse(saisie, out int numero) || numero < 1 || numero > echeances.Count)
    {
        Console.WriteLine("Numéro invalide.");
        return null;
    }

    return echeances[numero - 1];
}

// ===== Partie 1 : parcourir par échéance puis par strike =====   MODIFIÉ
void ParcourirOptions()
{
    DateTime? echeanceChoisie = ChoisirEcheance();
    if (echeanceChoisie == null)
    {
        return;
    }
    DateTime echeance = echeanceChoisie.Value;

    List<Option> optionsEcheance = options
        .Where(o => o.Echeance == echeance)
        .OrderBy(o => o.Strike)
        .ThenBy(o => o.Type)
        .ToList();

    Console.WriteLine();
    Console.WriteLine($"--- Échéance {echeance:dd/MM/yyyy} : {optionsEcheance.Count} options ---");
    foreach (Option o in optionsEcheance)
    {
        o.Afficher();

        double maturite = Black76.Maturite(o.Echeance, o.Horodatage);
        double theo = Black76.PrixBtc(o.Type, o.Future, o.Strike, maturite, o.MarkIv / 100);
        double ecart = theo - o.Mark;

        Console.WriteLine($" Théo {theo,7:F4}  écart avec le mark {ecart,8:F5}");

        double vol = o.MarkIv / 100;
        double delta = Black76.Delta(o.Type, o.Future, o.Strike, maturite, vol);
        double gamma = Black76.Gamma(o.Future, o.Strike, maturite, vol);
        double vega = Black76.Vega(o.Future, o.Strike, maturite, vol);
        double theta = Black76.Theta(o.Future, o.Strike, maturite, vol);

        Console.WriteLine($"   Delta {delta,6:F3}  Gamma {gamma,9:F6}  Vega {vega,7:F2}  Theta {theta,8:F2}");

        double volBid = Black76.VolImplicite(o.Type, o.Future, o.Strike, maturite, o.Bid);
        double volMark = Black76.VolImplicite(o.Type, o.Future, o.Strike, maturite, o.Mark);
        double volAsk = Black76.VolImplicite(o.Type, o.Future, o.Strike, maturite, o.Ask);

        Console.WriteLine($"    Vols : bid {TexteVol(volBid)}  mark {TexteVol(volMark)} (Deribit {o.MarkIv:F2})  ask {TexteVol(volAsk)}");
    }
}

// ===== Partie 2.1 : composer une structure =====   NOUVEAU
async Task ComposerStructure()
{
    // 1. Type de structure
    Console.WriteLine();
    Console.WriteLine("Type de structure :");
    Console.WriteLine("1. Call spread");
    Console.WriteLine("2. Put spread");
    Console.WriteLine("3. Butterfly en calls");
    Console.WriteLine("4. Butterfly en puts");
    Console.Write("Votre choix : ");
    string? choixType = Console.ReadLine();

    TypeStructure type;
    if (choixType == "1") { type = TypeStructure.CallSpread; }
    else if (choixType == "2") { type = TypeStructure.PutSpread; }
    else if (choixType == "3") { type = TypeStructure.ButterflyCall; }
    else if (choixType == "4") { type = TypeStructure.ButterflyPut; }
    else
    {
        Console.WriteLine("Type invalide.");
        return;
    }

    // 2. Échéance
    DateTime? echeanceChoisie = ChoisirEcheance();
    if (echeanceChoisie == null)
    {
        return;
    }
    DateTime echeance = echeanceChoisie.Value;

    // Aide : on affiche les strikes cotés pour cette échéance
    List<double> strikesCotes = options
        .Where(o => o.Echeance == echeance)
        .Select(o => o.Strike)
        .Distinct()
        .OrderBy(k => k)
        .ToList();
    Console.WriteLine("Strikes cotés : " + string.Join(", ", strikesCotes));

    // 3. Strikes
    Console.Write("Strikes (du plus petit au plus grand, séparés par des espaces) : ");
    string? texteStrikes = Console.ReadLine();
    if (texteStrikes == null)
    {
        return;
    }

    List<double> strikes = new List<double>();
    foreach (string morceau in texteStrikes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
    {
        if (!double.TryParse(morceau, out double strike))
        {
            Console.WriteLine($"Strike invalide : {morceau}");
            return;
        }
        strikes.Add(strike);
    }

    // 4. Sens
    Console.Write("Sens (A = achat, V = vente) : ");
    string? texteSens = Console.ReadLine();

    Sens sens;
    if (texteSens == "A" || texteSens == "a") { sens = Sens.Achat; }
    else if (texteSens == "V" || texteSens == "v") { sens = Sens.Vente; }
    else
    {
        Console.WriteLine("Sens invalide.");
        return;
    }

    // 5. Quantité
    Console.Write("Quantité (ex : 1 ou 0,5) : ");
    string? texteQuantite = Console.ReadLine();
    if (!double.TryParse(texteQuantite, out double quantite))
    {
        Console.WriteLine("Quantité invalide.");
        return;
    }

    // 6. Création par la Factory : les contrôles sont faits là-bas
    try
    {
        Structure structure = FactoryStructure.Creer(type, options, echeance, strikes, sens, quantite);
        Console.WriteLine();
        structure.Afficher();

        // Partie 2.2 : cotation en continu, rafraîchie toutes les 2 secondes
        await CoterEnContinu(structure);

        // Partie 2.3 : profil de P&L
        Console.Write("Afficher le profil de P&L ? (O/N) : ");
        string? reponse = Console.ReadLine();
        if (reponse == "O" || reponse == "o")
        {
            AfficherProfil(structure);
        }

        // Partie 3.1 : booking au prix exécutable affiché
        Console.WriteLine();
        if (!structure.EstTraitable())
        {
            Console.WriteLine("Structure non traitable : pas de booking possible.");
            return;
        }
        Console.Write($"Traiter cette structure au prix exécutable de {structure.PrixExecutableAchat():F4} BTC ? (O/N) : ");
        string? reponseTrade = Console.ReadLine();
        if (reponseTrade == "O" || reponseTrade == "o")
        {
            Trade trade = blotter.Booker(structure, sens, quantite);
            Console.WriteLine("Trade enregistré :");
            trade.Afficher();
        }
    }
    catch (Exception erreur)
    {
        Console.WriteLine($"Erreur : {erreur.Message}");
    }
}

// Affiche une vol en %, ou "impossible" si elle vaut -1
string TexteVol(double vol)
{
    if (vol < 0)
    {
        return "impossible";
    }
    return $"{vol * 100:F2}";
}

// ===== Partie 2.2 : cotation d'une structure =====
void AfficherCotation(Structure structure)
{
    Console.WriteLine();
    Console.WriteLine($"===== {structure.Nom}  ({DateTime.Now:HH:mm:ss}) =====");

    // Détail par jambe
    foreach (Jambe jambe in structure.Jambes)
    {
        Option o = jambe.Option;
        double theo = jambe.PrixTheoBtc();
        double ecart = theo - o.Mark;

        string alerte = "";
        if (Math.Abs(ecart) > 0.0001)
        {
            alerte = "  <-- ÉCART > 0,0001 BTC";
        }

        Console.WriteLine($"{jambe.Sens,-6} {jambe.Quantite,4} x {o.Nom}");
        Console.WriteLine($"   Théo {theo:F4} BTC ({jambe.PrixTheoUsd():F2} USD)   Mark {o.Mark:F4}   Écart {ecart:F5}{alerte}");
        Console.WriteLine($"   Vols : bid {ComparerVol(jambe.VolImplicite(o.Bid), o.BidIv)}" +
                          $"  mark {ComparerVol(jambe.VolImplicite(o.Mark), o.MarkIv)}" +
                          $"  ask {ComparerVol(jambe.VolImplicite(o.Ask), o.AskIv)}");
    }

    // Structure
    Console.WriteLine();
    Console.WriteLine($"Prix théorique : {structure.PrixTheoBtc():F4} BTC ({structure.PrixTheoUsd():F2} USD)   Mark : {structure.PrixMarkBtc():F4} BTC");

    if (structure.EstTraitable())
    {
        Console.WriteLine($"Exécutable à l'achat : {structure.PrixExecutableAchat():F4} BTC   à la vente : {structure.PrixExecutableVente():F4} BTC   Fourchette : {structure.Fourchette():F4} BTC");
    }
    else
    {
        Console.WriteLine("Structure NON TRAITABLE : une jambe n'a pas de bid ou pas d'ask.");
    }

    Console.WriteLine();
    Console.WriteLine("Grecques        Nous      Deribit");
    Console.WriteLine($"Delta     {structure.Delta(),10:F4} {structure.DeltaDeribit(),10:F4}");
    Console.WriteLine($"Gamma     {structure.Gamma(),10:F6} {structure.GammaDeribit(),10:F6}");
    Console.WriteLine($"Vega      {structure.Vega(),10:F2} {structure.VegaDeribit(),10:F2}");
    Console.WriteLine($"Theta     {structure.Theta(),10:F2} {structure.ThetaDeribit(),10:F2}");
}

// Affiche ma vol et celle de Deribit ; signale un écart de plus de 0,1 point
string ComparerVol(double maVol, double volDeribit)
{
    if (maVol < 0)
    {
        return "impossible";
    }

    string texte = $"{maVol * 100:F2} (Deribit {volDeribit:F2})";
    if (Math.Abs(maVol * 100 - volDeribit) > 0.1)
    {
        texte += " <-- écart > 0,1 pt";
    }
    return texte;
}

// ===== Partie 2.2 : cotation en continu =====
async Task CoterEnContinu(Structure structure)
{
    // Tant que l'utilisateur n'a appuyé sur aucune touche
    while (!Console.KeyAvailable)
    {
        foreach (Jambe jambe in structure.Jambes)
        {
            await client.MettreAJourTicker(jambe.Option);
        }

        Console.Clear();
        AfficherCotation(structure);
        Console.WriteLine();
        Console.WriteLine("Mise à jour toutes les 2 secondes. Appuyez sur une touche pour arrêter.");

        await Task.Delay(pause);
    }

    Console.ReadKey(true);   // on "consomme" la touche appuyée
}

// ===== Partie 2.3 : profil de P&L =====
void AfficherProfil(Structure structure)
{
    double future = structure.Jambes[0].Option.Future;
    double primeUsd = ProfilPnL.PrimeUsd(structure);

    Console.WriteLine();
    Console.WriteLine($"===== Profil de P&L : {structure.Nom} =====");
    Console.WriteLine($"Future actuel : {future:F2} USD   Prime payée : {primeUsd:F2} USD");
    Console.WriteLine();
    Console.WriteLine("Variation   Sous-jacent    P&L échéance   P&L aujourd'hui   (USD)");

    for (int pourcent = -30; pourcent <= 30; pourcent += 5)
    {
        double sousJacent = future * (1 + pourcent / 100.0);
        double pnlEcheance = ProfilPnL.PnlEcheance(structure, sousJacent, primeUsd);
        double pnlAujourdhui = ProfilPnL.PnlAujourdhui(structure, sousJacent, primeUsd);

        Console.WriteLine($"{pourcent,6} %  {sousJacent,12:F2}  {pnlEcheance,13:F2}  {pnlAujourdhui,16:F2}");
    }

    // Gain max, perte max, points morts (à l'échéance)
    Console.WriteLine();
    if (ProfilPnL.GainIllimite(structure, primeUsd))
    {
        Console.WriteLine("Gain maximal  : illimité");
    }
    else
    {
        Console.WriteLine($"Gain maximal  : {ProfilPnL.GainMax(structure, primeUsd):F2} USD");
    }

    if (ProfilPnL.PerteIllimitee(structure, primeUsd))
    {
        Console.WriteLine("Perte maximale : illimitée");
    }
    else
    {
        Console.WriteLine($"Perte maximale : {ProfilPnL.PerteMax(structure, primeUsd):F2} USD");
    }

    List<double> pointsMorts = ProfilPnL.PointsMorts(structure, primeUsd);
    if (pointsMorts.Count == 0)
    {
        Console.WriteLine("Point(s) mort(s) : aucun");
    }
    else
    {
        List<string> textes = new List<string>();
        foreach (double point in pointsMorts)
        {
            textes.Add($"{point:F2}");
        }
        Console.WriteLine("Point(s) mort(s) : " + string.Join("  /  ", textes) + " USD");
    }
}

// ===== Partie 3.1 : blotter =====
void AfficherBlotter()
{
    Console.WriteLine();
    Console.WriteLine("===== BLOTTER =====");
    if (blotter.Trades.Count == 0)
    {
        Console.WriteLine("Aucun trade.");
        return;
    }
    foreach (Trade trade in blotter.Trades)
    {
        trade.Afficher();
    }
}

// ===== Partie 3.1 : débouclage =====
void DeboucherTrade()
{
    Console.WriteLine();
    Console.WriteLine("Structures encore ouvertes :");
    int nbOuvertes = 0;
    foreach (Trade trade in blotter.TradesOrigine())
    {
        if (trade.QuantiteRestante > 0)
        {
            Console.WriteLine($"#{trade.Id}  {trade.Sens} {trade.NomStructure}  reste {trade.QuantiteRestante} / {trade.Quantite}");
            nbOuvertes++;
        }
    }
    if (nbOuvertes == 0)
    {
        Console.WriteLine("Aucune structure ouverte.");
        return;
    }

    Console.Write("Numéro du trade à déboucler : ");
    if (!int.TryParse(Console.ReadLine(), out int id))
    {
        Console.WriteLine("Numéro invalide.");
        return;
    }

    Console.Write("Quantité à déboucler (ex : 1 ou 0,5) : ");
    if (!double.TryParse(Console.ReadLine(), out double quantite))
    {
        Console.WriteLine("Quantité invalide.");
        return;
    }

    try
    {
        Trade debouclage = blotter.Deboucler(id, quantite);
        Console.WriteLine("Débouclage enregistré :");
        debouclage.Afficher();
    }
    catch (Exception erreur)
    {
        Console.WriteLine($"Erreur : {erreur.Message}");
    }
}

// ===== Partie 3.2 : suivi du book en continu =====
async Task SuiviBook()
{
    if (blotter.Trades.Count == 0)
    {
        Console.WriteLine("Le book est vide : traitez d'abord une structure (menu 2).");
        return;
    }

    try
    {
        book.PrixReglement = await client.PrixReglement("BTC");

        while (!Console.KeyAvailable)
        {
            await client.MettreAJourPrix(options, "BTC");      // 1 appel pour tous les prix
            double indice = await client.PrixIndice("BTC");    // 1 appel pour l'indice

            Console.Clear();
            AfficherBook(indice);
            Console.WriteLine();
            Console.WriteLine("Mise à jour toutes les 2 secondes. Appuyez sur une touche pour arrêter.");

            await Task.Delay(pause);
        }
        Console.ReadKey(true);
    }
    catch (Exception erreur)
    {
        Console.WriteLine($"Erreur : {erreur.Message}");
    }
}

void AfficherBook(double indice)
{
    Console.WriteLine($"===== BOOK  ({DateTime.Now:HH:mm:ss})   Indice BTC : {indice:F2} USD =====");
    Console.WriteLine();
    Console.WriteLine("Trade  Structure                                  Reste   Valeur mark  Valeur théo     P&L (BTC)     P&L (USD)");

    foreach (Trade origine in blotter.TradesOrigine())
    {
        string etat = "";
        if (origine.QuantiteRestante <= 0)
        {
            etat = " (fermée)";
        }
        else if (book.EstEchue(origine.Lignes[0].Option))
        {
            etat = " (échue, réglée)";
        }

        string libelle = $"{origine.Sens} {origine.NomStructure}{etat}";
        double valeurMark = book.ValeurBtc(origine, false);
        double valeurTheo = book.ValeurBtc(origine, true);
        double pnl = book.PnlBtc(origine, false);

        Console.WriteLine($"#{origine.Id,-5} {libelle,-42} {origine.QuantiteRestante,5}  {valeurMark,11:F4}  {valeurTheo,11:F4}  {pnl,12:F4}  {pnl * indice,12:F2}");
    }

    double pnlTotal = book.PnlTotalBtc(false);
    Console.WriteLine();
    Console.WriteLine($"TOTAL{"",44}  {book.ValeurTotaleBtc(false),11:F4}  {book.ValeurTotaleBtc(true),11:F4}  {pnlTotal,12:F4}  {pnlTotal * indice,12:F2}");
    Console.WriteLine($"P&L total au prix théorique : {book.PnlTotalBtc(true):F4} BTC ({book.PnlTotalBtc(true) * indice:F2} USD)");

    // Grecques par échéance et total
    Console.WriteLine();
    Console.WriteLine("Échéance         Delta        Gamma       Vega      Theta");
    foreach (DateTime echeance in book.EcheancesOuvertes())
    {
        double[] g = book.Grecques(echeance);
        Console.WriteLine($"{echeance:dd/MM/yyyy}  {g[0],10:F4}  {g[1],11:F6}  {g[2],9:F2}  {g[3],9:F2}");
    }
    double[] total = book.Grecques(null);
    Console.WriteLine($"TOTAL       {total[0],10:F4}  {total[1],11:F6}  {total[2],9:F2}  {total[3],9:F2}");
}

// ===== Partie 3.3 : exports =====
async Task Exporter()
{
    if (blotter.Trades.Count == 0)
    {
        Console.WriteLine("Rien à exporter : le blotter est vide.");
        return;
    }

    // Dossier "Exports" à côté de la solution
    string dossier = Path.GetFullPath("Exports");
    Directory.CreateDirectory(dossier);

    double indice;
    try
    {
        // Valeurs à jour au moment de l'export
        await client.MettreAJourPrix(options, "BTC");
        indice = await client.PrixIndice("BTC");
        book.PrixReglement = await client.PrixReglement("BTC");

        Export.ExporterCsv(blotter, book, indice, dossier);
        Console.WriteLine($"CSV exportés dans {dossier} : blotter.csv et book.csv");
    }
    catch (Exception erreur)
    {
        Console.WriteLine($"Erreur pendant l'export CSV : {erreur.Message}");
        return;
    }

    try
    {
        string fichier = Export.ExporterExcel(blotter, book, indice, dossier);
        Console.WriteLine($"Classeur Excel créé et ouvert : {fichier}");
    }
    catch (Exception erreur)
    {
        Console.WriteLine($"Export Excel impossible (Excel est-il installé ?) : {erreur.Message}");
    }
}
