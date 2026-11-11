using System.Text.Json;

namespace DeribitPricer.Metier;

// ===== Partie 4 : enregistrement et rejeu =====

// Le contrat : « donne-moi la réponse JSON de cette adresse Deribit ».
// ClientDeribit ne sait pas si la réponse vient d'Internet ou d'un fichier.
public interface ISourceDonnees
{
    Task<string> Lire(string url);
}

// Une réponse enregistrée : une ligne du fichier
public class LigneEnregistree
{
    public long Temps { get; set; }          // heure de réception (millisecondes depuis 1970)
    public string Url { get; set; } = "";
    public string Json { get; set; } = "";
}

// Mode live : on interroge Deribit, et on enregistre si un fichier est donné
public class SourceLive : ISourceDonnees
{
    private HttpClient _http = new HttpClient();
    private string? _fichierEnregistrement;   // null = pas d'enregistrement

    public SourceLive(string? fichierEnregistrement)
    {
        _fichierEnregistrement = fichierEnregistrement;
    }

    public async Task<string> Lire(string url)
    {
        string json = await _http.GetStringAsync(url);

        if (_fichierEnregistrement != null)
        {
            LigneEnregistree ligne = new LigneEnregistree();
            ligne.Temps = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            ligne.Url = url;
            ligne.Json = json;

            // Une réponse par ligne, ajoutée à la fin du fichier
            File.AppendAllText(_fichierEnregistrement, JsonSerializer.Serialize(ligne) + Environment.NewLine);
        }

        return json;
    }
}

// Mode rejeu : on relit le fichier, dans l'ordre d'origine, sans Internet
public class SourceRejeu : ISourceDonnees
{
    private List<LigneEnregistree> _lignes;
    private int _position;   // où on en est dans le fichier

    public SourceRejeu(string fichier)
    {
        if (!File.Exists(fichier))
        {
            throw new Exception($"Fichier d'enregistrement introuvable : {fichier}");
        }

        _lignes = new List<LigneEnregistree>();
        _position = 0;

        foreach (string texte in File.ReadAllLines(fichier))
        {
            if (texte.Trim() == "")
            {
                continue;
            }
            LigneEnregistree? ligne = JsonSerializer.Deserialize<LigneEnregistree>(texte);
            if (ligne != null)
            {
                _lignes.Add(ligne);
            }
        }

        if (_lignes.Count == 0)
        {
            throw new Exception("Le fichier d'enregistrement est vide.");
        }
    }

    public Task<string> Lire(string url)
    {
        // 1. La prochaine réponse enregistrée pour cette adresse : le marché « avance »
        for (int i = _position; i < _lignes.Count; i++)
        {
            if (_lignes[i].Url == url)
            {
                _position = i + 1;
                return Task.FromResult(_lignes[i].Json);
            }
        }

        // 2. Fin de l'enregistrement pour cette adresse : on garde la dernière réponse connue
        for (int i = _lignes.Count - 1; i >= 0; i--)
        {
            if (_lignes[i].Url == url)
            {
                return Task.FromResult(_lignes[i].Json);
            }
        }

        throw new Exception($"Aucune donnée enregistrée pour : {url}");
    }
}
