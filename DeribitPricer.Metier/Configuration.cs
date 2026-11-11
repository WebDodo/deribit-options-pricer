using System.Text.Json;

namespace DeribitPricer.Metier;

// Contenu de appsettings.json : on change de mode sans toucher au code
public class Configuration
{
    public string Mode { get; set; } = "Live";            // "Live", "Enregistrement" ou "Rejeu"
    public string Fichier { get; set; } = "seance.jsonl";  // fichier d'enregistrement
    public int Vitesse { get; set; } = 1;                  // rejeu : 1 = vitesse d'origine, 10 = dix fois plus vite

    public static Configuration Charger()
    {
        string chemin = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(chemin))
        {
            return new Configuration();   // pas de fichier : mode live par défaut
        }

        string json = File.ReadAllText(chemin);
        Configuration? config = JsonSerializer.Deserialize<Configuration>(json);
        if (config == null)
        {
            return new Configuration();
        }

        if (config.Vitesse < 1)
        {
            config.Vitesse = 1;
        }
        return config;
    }
}
