# Pricer live Deribit

Outil C# (.NET 10, application console) qui cote en temps réel des stratégies d'options BTC sur **Deribit**, enregistre les trades dans un blotter, suit le risque du book, exporte en CSV / Excel et peut rejouer une séance sans connexion.

Projet réalisé dans le cadre du M2 EIF (cours *C# pour la finance*) par **Doryan Lochet** et **Steeve Cheng**.

---

## Fonctionnalités

- **Référentiel** : chargement des ~900 options BTC cotées, parcours par échéance puis par strike (bid, ask, mark, vol).
- **Pricer Black-76 maison** : prix en USD et en BTC, grecques (delta, gamma, vega, theta), vol implicite par Newton avec repli sur dichotomie.
- **Structures** : call spread, put spread, butterfly (calls ou puts), avec contrôle des saisies (strike non coté, ordre, symétrie, pas de quantité).
- **Cotation en continu** (toutes les 2 s) : prix théorique vs mark, vols implicites vs Deribit, prix exécutable, fourchette, grecques comparées à Deribit.
- **Profil de P&L** de −30 % à +30 % (à l'échéance et aujourd'hui), gain max, perte max, points morts.
- **Booking et blotter** : trade au prix exécutable, débouclage total ou partiel lié au trade d'origine.
- **Suivi du book** en continu : valeurs au mark et au théorique, P&L en BTC et en USD, grecques par échéance, règlement des options échues.
- **Exports** : `blotter.csv`, `book.csv` et un classeur Excel mis en forme (Synthèse, Book, Blotter).
- **Enregistrement / rejeu** : mode choisi dans `appsettings.json`, sans modifier le code.

## Prérequis

- [SDK .NET 10](https://dotnet.microsoft.com/download)
- Windows avec Microsoft Excel installé, **uniquement** pour l'export du classeur Excel (les CSV fonctionnent partout)
- Une connexion Internet en mode live (API publique Deribit, sans compte)

## Lancer le projet

```bash
git clone <url-du-depot>
cd <dossier-du-depot>

dotnet build
dotnet test
dotnet run --project DeribitPricer.App
```

Les bibliothèques NuGet sont téléchargées automatiquement au premier `dotnet build`.

## Configuration : live, enregistrement ou rejeu

Fichier `DeribitPricer.App/appsettings.json` :

```json
{
  "Mode": "Live",
  "Fichier": "seance.jsonl",
  "Vitesse": 1
}
```

| `Mode` | Comportement |
| --- | --- |
| `Live` | Interroge Deribit en direct |
| `Enregistrement` | Live, et enregistre chaque réponse de Deribit dans `Fichier` |
| `Rejeu` | Rejoue `Fichier` sans connexion, `Vitesse` fois plus vite |

## Menus

| Menu | Action |
| --- | --- |
| 1 | Parcourir les options |
| 2 | Composer, coter et traiter une structure |
| 3 | Afficher le blotter |
| 4 | Déboucler un trade |
| 5 | Suivi du book en continu |
| 6 | Exporter le blotter et le book (CSV + Excel) |
| 0 | Quitter |

## Architecture

```
DeribitPricer.slnx
├── DeribitPricer.App        Application console : menus, saisies, affichage
├── DeribitPricer.Metier     Bibliothèque : toute la logique métier
│   ├── Option.cs            Une option cotée
│   ├── ClientDeribit.cs     Appels à l'API Deribit, lecture du JSON
│   ├── SourceDonnees.cs     ISourceDonnees : SourceLive / SourceRejeu
│   ├── Configuration.cs     Lecture de appsettings.json
│   ├── Black76.cs           Prix, grecques, vol implicite
│   ├── Jambe.cs, Structure.cs
│   ├── FactoryStructure.cs  Pattern Factory + contrôles de saisie
│   ├── ProfilPnL.cs         Profil de P&L, points morts
│   ├── Trade.cs, Blotter.cs Trades, booking, débouclage
│   ├── Book.cs              Valeurs, P&L, grecques
│   └── Export.cs            CSV (CsvHelper) et Excel (COM Interop)
└── DeribitPricer.Tests      21 tests unitaires MSTest
```

Choix principaux : **pattern Factory** pour créer les structures, **interface `ISourceDonnees`** pour passer du live au rejeu sans toucher au reste du code, et un **book recalculé à partir du blotter** (aucune donnée dupliquée).

## Tests

```bash
dotnet test
```

21 tests, sans réseau, sur un marché fictif : formules Black-76 (parité call-put, valeur connue), vol implicite, contrôles de la Factory, prix exécutable, booking, débouclage, P&L du book et profil de P&L.

## Dépendances

| Package NuGet | Version | Usage |
| --- | --- | --- |
| MathNet.Numerics | 5.0.0 | Loi normale |
| CsvHelper | 33.0.1 | Exports CSV |
| Microsoft.Office.Interop.Excel | 16.0.18925 | Export Excel |
| MSTest | 4.0.2 | Tests unitaires |

## Limites

L'option sur mesure (strike et échéance libres, interpolés) et sa couverture delta / gamma / vega ne sont pas implémentées.

## Données

Données de marché issues de l'[API publique Deribit](https://docs.deribit.com). Projet pédagogique, sans aucun ordre réel envoyé.
