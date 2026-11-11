using System.Globalization;
using System.Text.Json;

namespace DeribitPricer.Metier;

public class ClientDeribit
{
    // Champs privés
    private ISourceDonnees _source;   // Internet (live) ou fichier (rejeu) : ClientDeribit ne fait pas la différence
    private string _urlBase = "https://www.deribit.com/api/v2/public/";

    public ClientDeribit(ISourceDonnees source)
    {
        _source = source;
    }

    // Charge toutes les options cotées d'une devise (ex : "BTC")
    public async Task<List<Option>> ChargerOptions(string devise)
    {
        // 1. On envoie la requête et on reçoit le texte JSON
        string url = _urlBase + "get_instruments?currency=" + devise + "&kind=option&expired=false";
        string json = await _source.Lire(url);

        // 2. On lit le JSON et on va dans "result"
        JsonDocument document = JsonDocument.Parse(json);
        JsonElement result = document.RootElement.GetProperty("result");

        // 3. On crée un objet Option pour chaque élément de la liste
        List<Option> options = new List<Option>();

        foreach (JsonElement element in result.EnumerateArray())
        {
            string nom = element.GetProperty("instrument_name").ToString();
            double strike = element.GetProperty("strike").GetDouble();
            double quantiteMin = element.GetProperty("min_trade_amount").GetDouble();

            TypeOption type;
            if (element.GetProperty("option_type").ToString() == "call")
            {
                type = TypeOption.Call;
            }
            else
            {
                type = TypeOption.Put;
            }

            long timestamp = element.GetProperty("expiration_timestamp").GetInt64();
            DateTime echeance = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;

            Option option = new Option(nom, type, strike, echeance, quantiteMin);
            options.Add(option);
        }

        return options;
    }

    // Remplit les prix (bid, ask, mark, vol, future) des options déjà chargées
    public async Task MettreAJourPrix(List<Option> options , string devise)
    {
        // 1. Un seul appel pour avoir les prix de TOUTES les options
        string url = _urlBase + "get_book_summary_by_currency?currency=" + devise + "&kind=option";
        string json = await _source.Lire(url);

        JsonDocument document = JsonDocument.Parse(json);
        JsonElement result = document.RootElement.GetProperty("result");

        // 2. Dictionnaire nom -> option, pour retrouver une option rapidement par son nom
        Dictionary<string, Option> optionsParNom = new Dictionary<string, Option>();
        foreach (Option option in options)
        {
            optionsParNom[option.Nom] = option;
        }

        // 3. Pour chaque prix reçu, on remplit l'option correspondante
        foreach (JsonElement element in result.EnumerateArray())
        {
            string nom = element.GetProperty("instrument_name").ToString();

            if (optionsParNom.ContainsKey(nom))
            {
                Option option = optionsParNom[nom];
                option.Bid = LireNombre(element, "bid_price");
                option.Ask = LireNombre(element, "ask_price");
                option.Mark = LireNombre(element, "mark_price");
                option.MarkIv = LireNombre(element, "mark_iv");
                option.Future = LireNombre(element, "underlying_price");

                // Horodatage de la cotation : sert au calcul de la maturité
                if (element.TryGetProperty("creation_timestamp", out JsonElement horodatage))
                {
                    option.Horodatage = DateTimeOffset.FromUnixTimeMilliseconds(horodatage.GetInt64()).UtcDateTime;
                }
                else
                {
                    option.Horodatage = DateTime.UtcNow;
                }
            }
        }
    }

    // Met à jour UNE option avec le ticker Deribit : prix, vols, grecques, horodatage
    public async Task MettreAJourTicker(Option option)
    {
        string url = _urlBase + "ticker?instrument_name=" + option.Nom;
        string json = await _source.Lire(url);

        JsonDocument document = JsonDocument.Parse(json);
        JsonElement result = document.RootElement.GetProperty("result");

        // Prix et vols
        option.Bid = LireNombre(result, "best_bid_price");
        option.Ask = LireNombre(result, "best_ask_price");
        option.Mark = LireNombre(result, "mark_price");
        option.MarkIv = LireNombre(result, "mark_iv");
        option.BidIv = LireNombre(result, "bid_iv");
        option.AskIv = LireNombre(result, "ask_iv");
        option.Future = LireNombre(result, "underlying_price");

        // Grecques de Deribit : elles sont dans un sous-bloc "greeks"
        JsonElement greeks = result.GetProperty("greeks");
        option.DeltaDeribit = LireNombre(greeks, "delta");
        option.GammaDeribit = LireNombre(greeks, "gamma");
        option.VegaDeribit = LireNombre(greeks, "vega");
        option.ThetaDeribit = LireNombre(greeks, "theta");

        // Horodatage de la cotation (millisecondes depuis 1970)
        long timestamp = result.GetProperty("timestamp").GetInt64();
        option.Horodatage = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;
    }

    // Indice de prix spot (ex : btc_usd) : sert à convertir le P&L en USD
    public async Task<double> PrixIndice(string devise)
    {
        string url = _urlBase + "get_index_price?index_name=" + devise.ToLower() + "_usd";
        string json = await _source.Lire(url);

        JsonDocument document = JsonDocument.Parse(json);
        JsonElement result = document.RootElement.GetProperty("result");
        return LireNombre(result, "index_price");
    }

    // Prix de règlement des dernières échéances : jour -> prix de règlement
    public async Task<Dictionary<DateTime, double>> PrixReglement(string devise)
    {
        string url = _urlBase + "get_delivery_prices?index_name=" + devise.ToLower() + "_usd&count=30";
        string json = await _source.Lire(url);

        JsonDocument document = JsonDocument.Parse(json);
        JsonElement data = document.RootElement.GetProperty("result").GetProperty("data");

        Dictionary<DateTime, double> prix = new Dictionary<DateTime, double>();
        foreach (JsonElement element in data.EnumerateArray())
        {
            DateTime jour = DateTime.ParseExact(element.GetProperty("date").ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            prix[jour] = LireNombre(element, "delivery_price");
        }
        return prix;
    }

    // Lit un nombre dans le JSON ; renvoie 0 si le champ est absent ou vide (null)
    private double LireNombre(JsonElement element, string champ)
    {
        if (!element.TryGetProperty(champ, out JsonElement valeur))
        {
            return 0;
        }

        if (valeur.ValueKind == JsonValueKind.Null)
        {
            return 0;
        }

        return valeur.GetDouble();
    }
}
