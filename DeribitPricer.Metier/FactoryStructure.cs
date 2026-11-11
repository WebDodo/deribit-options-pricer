namespace DeribitPricer.Metier;

public enum TypeStructure
{
    CallSpread,
    PutSpread,
    ButterflyCall,
    ButterflyPut
}

public static class FactoryStructure
{
    // Crée une structure à partir de la saisie de l'utilisateur.
    // Lève une exception avec un message clair si la saisie est incohérente.
    public static Structure Creer(TypeStructure type, List<Option> options, DateTime echeance,
                                  List<double> strikes, Sens sens, double quantite)
    {
        // 1. Contrôles sur les strikes (nombre, ordre, symétrie)
        VerifierStrikes(type, strikes);

        // 2. Sens des jambes : "longue" = le sens choisi, "courte" = l'inverse
        Sens sensLong = sens;
        Sens sensCourt = Inverser(sens);

        // 3. Création de la bonne structure
        Structure structure;

        switch (type)
        {
            case TypeStructure.CallSpread:
                // achat call K1, vente call K2
                structure = new Structure($"Call spread {strikes[0]}/{strikes[1]}");
                structure.AjouterJambe(TrouverOption(options, echeance, strikes[0], TypeOption.Call), sensLong, quantite);
                structure.AjouterJambe(TrouverOption(options, echeance, strikes[1], TypeOption.Call), sensCourt, quantite);
                break;

            case TypeStructure.PutSpread:
                // achat put K2, vente put K1
                structure = new Structure($"Put spread {strikes[0]}/{strikes[1]}");
                structure.AjouterJambe(TrouverOption(options, echeance, strikes[1], TypeOption.Put), sensLong, quantite);
                structure.AjouterJambe(TrouverOption(options, echeance, strikes[0], TypeOption.Put), sensCourt, quantite);
                break;

            case TypeStructure.ButterflyCall:
                // achat K1, vente 2 x K2, achat K3
                structure = new Structure($"Butterfly call {strikes[0]}/{strikes[1]}/{strikes[2]}");
                structure.AjouterJambe(TrouverOption(options, echeance, strikes[0], TypeOption.Call), sensLong, quantite);
                structure.AjouterJambe(TrouverOption(options, echeance, strikes[1], TypeOption.Call), sensCourt, 2 * quantite);
                structure.AjouterJambe(TrouverOption(options, echeance, strikes[2], TypeOption.Call), sensLong, quantite);
                break;

            case TypeStructure.ButterflyPut:
                structure = new Structure($"Butterfly put {strikes[0]}/{strikes[1]}/{strikes[2]}");
                structure.AjouterJambe(TrouverOption(options, echeance, strikes[0], TypeOption.Put), sensLong, quantite);
                structure.AjouterJambe(TrouverOption(options, echeance, strikes[1], TypeOption.Put), sensCourt, 2 * quantite);
                structure.AjouterJambe(TrouverOption(options, echeance, strikes[2], TypeOption.Put), sensLong, quantite);
                break;

            default:
                throw new Exception("Type de structure non pris en charge.");
        }

        // 4. Contrôle de la quantité (pas de cotation de l'instrument)
        VerifierQuantite(quantite, structure.Jambes[0].Option.QuantiteMin);

        return structure;
    }

    // Nombre de strikes, ordre croissant, symétrie du butterfly
    private static void VerifierStrikes(TypeStructure type, List<double> strikes)
    {
        int nbAttendu = 2;
        if (type == TypeStructure.ButterflyCall || type == TypeStructure.ButterflyPut)
        {
            nbAttendu = 3;
        }

        if (strikes.Count != nbAttendu)
        {
            throw new Exception($"Cette structure demande {nbAttendu} strikes.");
        }

        for (int i = 1; i < strikes.Count; i++)
        {
            if (strikes[i] <= strikes[i - 1])
            {
                throw new Exception("Les strikes doivent être saisis dans l'ordre croissant (K1 < K2 < K3).");
            }
        }

        if (nbAttendu == 3 && strikes[1] - strikes[0] != strikes[2] - strikes[1])
        {
            throw new Exception("Butterfly non symétrique : K2 - K1 doit être égal à K3 - K2.");
        }
    }

    // Cherche l'option cotée correspondante ; erreur si elle n'existe pas
    private static Option TrouverOption(List<Option> options, DateTime echeance, double strike, TypeOption type)
    {
        foreach (Option option in options)
        {
            if (option.Echeance == echeance && option.Strike == strike && option.Type == type)
            {
                return option;
            }
        }

        throw new Exception($"Le strike {strike} ({type}) n'est pas coté pour l'échéance du {echeance:dd/MM/yyyy}.");
    }

    // La quantité doit être positive et un multiple du pas de cotation
    private static void VerifierQuantite(double quantite, double pas)
    {
        if (quantite <= 0)
        {
            throw new Exception("La quantité doit être strictement positive.");
        }

        double nombreDePas = quantite / pas;
        if (Math.Abs(nombreDePas - Math.Round(nombreDePas)) > 1e-9)
        {
            throw new Exception($"La quantité {quantite} n'est pas un multiple du pas de cotation ({pas}).");
        }
    }

    // Achat devient Vente, Vente devient Achat
    private static Sens Inverser(Sens sens)
    {
        if (sens == Sens.Achat)
        {
            return Sens.Vente;
        }
        return Sens.Achat;
    }
}