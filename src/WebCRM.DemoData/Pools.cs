using System.Globalization;
using System.Text;

namespace WebCRM.DemoData;

/// <summary>
/// Hand-written Greek (and some foreign) data with proper accents. Bogus supplies English and German people and
/// companies and Greek person and company names, but its Greek cities, phones and emails are not usable.
/// </summary>
public static class Pools
{
    // ---- People ----

    public static readonly string[] GreekMaleFirstNames =
    [
        "Γιώργος", "Κώστας", "Δημήτρης", "Νίκος", "Γιάννης", "Παναγιώτης", "Χρήστος", "Αντώνης", "Μιχάλης", "Θανάσης",
        "Σταύρος", "Βασίλης", "Στέφανος", "Αλέξανδρος", "Πέτρος", "Ευάγγελος", "Λευτέρης", "Σπύρος", "Μανώλης", "Θοδωρής",
        "Άρης", "Ορέστης", "Ηλίας", "Γρηγόρης", "Τάσος", "Φώτης", "Μάρκος", "Διονύσης", "Θεόδωρος", "Κυριάκος",
    ];

    public static readonly string[] GreekFemaleFirstNames =
    [
        "Μαρία", "Ελένη", "Αικατερίνη", "Σοφία", "Δήμητρα", "Γεωργία", "Αναστασία", "Ειρήνη", "Χριστίνα", "Παναγιώτα",
        "Κωνσταντίνα", "Βασιλική", "Αγγελική", "Ευαγγελία", "Θεοδώρα", "Ιωάννα", "Φωτεινή", "Νίκη", "Ζωή", "Αθηνά",
        "Στέλλα", "Ευτυχία", "Αργυρώ", "Δάφνη", "Μυρτώ", "Κλεοπάτρα", "Ραλλού", "Σμαράγδα", "Λίνα", "Μαργαρίτα",
    ];

    /// <summary>Male and female form of the same surname (the female form is also the genitive of the male one).</summary>
    public static readonly (string Male, string Female)[] GreekSurnames =
    [
        ("Παπαδόπουλος", "Παπαδοπούλου"), ("Καραγιάννης", "Καραγιάννη"), ("Μακρής", "Μακρή"), ("Λαμπράκης", "Λαμπράκη"),
        ("Δημητρακόπουλος", "Δημητρακοπούλου"), ("Αναστασόπουλος", "Αναστασοπούλου"), ("Θεοδωρόπουλος", "Θεοδωροπούλου"),
        ("Ξενάκης", "Ξενάκη"), ("Μαυρίδης", "Μαυρίδου"), ("Παναγιωτόπουλος", "Παναγιωτοπούλου"), ("Σταμούλης", "Σταμούλη"),
        ("Καλογεράς", "Καλογερά"), ("Βλάχος", "Βλάχου"), ("Κατσαρός", "Κατσαρού"), ("Τσακίρης", "Τσακίρη"), ("Χατζής", "Χατζή"),
        ("Μπακάλης", "Μπακάλη"), ("Λέκκας", "Λέκκα"), ("Ρήγας", "Ρήγα"), ("Σπυρόπουλος", "Σπυροπούλου"),
        ("Μιχαηλίδης", "Μιχαηλίδου"), ("Καρράς", "Καρρά"), ("Τζανετάκης", "Τζανετάκη"), ("Βαρελάς", "Βαρελά"),
        ("Λυμπερόπουλος", "Λυμπεροπούλου"), ("Ντόκας", "Ντόκα"), ("Πανταζής", "Πανταζή"), ("Στεφανίδης", "Στεφανίδου"),
        ("Γεωργίου", "Γεωργίου"), ("Ιωάννου", "Ιωάννου"), ("Νικολάου", "Νικολάου"), ("Κωνσταντίνου", "Κωνσταντίνου"),
        ("Δημητρίου", "Δημητρίου"), ("Παπαδημητρίου", "Παπαδημητρίου"), ("Αντωνίου", "Αντωνίου"), ("Οικονόμου", "Οικονόμου"),
        ("Παπαγεωργίου", "Παπαγεωργίου"), ("Παπανικολάου", "Παπανικολάου"), ("Αλεξίου", "Αλεξίου"), ("Βασιλείου", "Βασιλείου"),
        ("Ζαχαρίου", "Ζαχαρίου"), ("Φωτίου", "Φωτίου"), ("Αθανασίου", "Αθανασίου"), ("Σωτηρίου", "Σωτηρίου"),
        ("Ευαγγέλου", "Ευαγγέλου"), ("Παπαϊωάννου", "Παπαϊωάννου"), ("Γαλάνης", "Γαλάνη"), ("Κοντός", "Κοντού"),
        ("Βούλγαρης", "Βούλγαρη"), ("Ζερβός", "Ζερβού"),
    ];

    public static readonly string[] Departments =
    [
        "Purchasing", "Προμήθειες", "Finance", "Οικονομικό", "IT", "Πληροφορική", "Sales", "Πωλήσεις", "Operations",
        "Λειτουργίες", "Marketing", "Human Resources", "Ανθρώπινο Δυναμικό", "Logistics", "Management", "Διοίκηση",
    ];

    public static readonly string[] JobTitles =
    [
        "Purchasing Manager", "Υπεύθυνος Προμηθειών", "Financial Director", "Οικονομικός Διευθυντής", "IT Manager",
        "Διευθυντής Πληροφορικής", "Sales Director", "Διευθυντής Πωλήσεων", "Operations Manager", "CEO", "Γενικός Διευθυντής",
        "Owner", "Ιδιοκτήτης", "Office Manager", "Υπεύθυνος Γραφείου", "Accountant", "Λογιστής", "Procurement Specialist",
        "Marketing Manager", "Project Manager", "Διευθυντής Έργων", "Buyer", "Technical Manager", "Τεχνικός Διευθυντής",
    ];

    // ---- Places ----

    /// <summary>Greek city, first digits of its postcodes and the area code (so that area code and number are 10 digits).</summary>
    public static readonly (string City, string PostcodePrefix, string AreaCode)[] GreekCities =
    [
        ("Αθήνα", "10", "210"), ("Θεσσαλονίκη", "54", "2310"), ("Πάτρα", "26", "2610"), ("Ηράκλειο", "71", "2810"),
        ("Λάρισα", "41", "2410"), ("Βόλος", "38", "24210"), ("Ιωάννινα", "45", "26510"), ("Καβάλα", "65", "2510"),
        ("Χανιά", "73", "28210"), ("Ρόδος", "85", "22410"), ("Κηφισιά", "14", "210"), ("Μαρούσι", "15", "210"),
        ("Γλυφάδα", "16", "210"), ("Πειραιάς", "18", "210"), ("Καλαμάτα", "24", "27210"), ("Σέρρες", "62", "23210"),
        ("Αλεξανδρούπολη", "68", "25510"), ("Κομοτηνή", "69", "25310"), ("Τρίκαλα", "42", "24310"), ("Λαμία", "35", "22310"),
        ("Κέρκυρα", "49", "26610"), ("Χαλάνδρι", "15", "210"), ("Περιστέρι", "12", "210"), ("Νέα Σμύρνη", "17", "210"),
    ];

    /// <summary>Weights for picking a Greek city: Athens and Thessaloniki dominate, as they do in real data.</summary>
    public static readonly int[] GreekCityWeights =
        [30, 18, 6, 5, 4, 3, 2, 2, 2, 2, 5, 3, 2, 4, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2];

    public static readonly string[] GreekStreets =
    [
        "Ερμού", "Λεωφόρος Κηφισίας", "Τσιμισκή", "Σταδίου", "Πανεπιστημίου", "Βασιλίσσης Σοφίας", "Λεωφόρος Συγγρού",
        "Αγίου Δημητρίου", "Εθνικής Αντιστάσεως", "Μητροπόλεως", "Κολοκοτρώνη", "Πατησίων", "Αριστοτέλους", "Εγνατία",
        "Ηρακλείου", "Λεωφόρος Αλεξάνδρας", "Μιαούλη", "Καποδιστρίου", "Δελφών", "Αμαλίας", "Μεσογείων", "Θησέως",
    ];

    public static readonly string[] EnglishStreets =
    [
        "High Street", "Station Road", "Main Street", "Park Lane", "Church Road", "Market Square", "Victoria Road", "King Street",
    ];

    public static readonly string[] TaxOffices =
    [
        "ΔΟΥ Αθηνών", "ΔΟΥ Κηφισιάς", "ΔΟΥ Α΄ Θεσσαλονίκης", "ΔΟΥ Γλυφάδας", "ΔΟΥ Πατρών", "ΔΟΥ Ηρακλείου", "ΔΟΥ Λαρίσης",
        "ΔΟΥ Πειραιά", "ΔΟΥ Χαλανδρίου", "ΔΟΥ Μαρουσίου", "ΔΟΥ Βόλου", "ΔΟΥ Καβάλας", "ΔΟΥ Περιστερίου", "ΔΟΥ Ιωαννίνων",
    ];

    /// <summary>Foreign market: country code, city, postcode shape ("#" = digit, "A" = letter), phone prefix, tax id prefix.</summary>
    public static readonly (string Country, string City, string PostcodeShape, string PhonePrefix, string VatPrefix)[] ForeignMarkets =
    [
        ("DE", "Berlin", "#####", "+49 30", "DE"), ("DE", "München", "#####", "+49 89", "DE"), ("DE", "Hamburg", "#####", "+49 40", "DE"),
        ("GB", "London", "A# #AA", "+44 20", "GB"), ("GB", "Manchester", "A# #AA", "+44 161", "GB"),
        ("FR", "Paris", "750##", "+33 1", "FR"), ("IT", "Milano", "201##", "+39 02", "IT"), ("NL", "Amsterdam", "#### AA", "+31 20", "NL"),
        ("CY", "Λευκωσία", "1###", "+357 22", "CY"), ("CY", "Λεμεσός", "3###", "+357 25", "CY"),
    ];

    // ---- Companies ----

    public static readonly string[] GreekCompanyForms = ["Α.Ε.", "Ε.Π.Ε.", "Ο.Ε.", "Ι.Κ.Ε.", "Μ.Ε.Π.Ε."];

    /// <summary>Business words; the first of each pair is a nominative plural/adjective form used before the family name.</summary>
    public static readonly string[] GreekBusinessWords =
    [
        "Μεταφορές", "Τρόφιμα", "Κατασκευές", "Εμπορική", "Βιομηχανία", "Ξενοδοχεία", "Ενέργεια", "Τεχνική", "Πληροφορική",
        "Συμβουλευτική", "Φαρμακευτική", "Εκδόσεις", "Αναψυκτήρια", "Ναυτιλιακή", "Ασφαλιστική", "Επισκευές", "Έπιπλα",
        "Οινοποιία", "Αγροτικά Προϊόντα", "Υδραυλικά", "Ηλεκτρολογικά", "Λογιστικές Υπηρεσίες", "Εκπαίδευση", "Τουριστική",
    ];

    public static readonly string[] EnglishBusinessWords =
    [
        "Logistics", "Consulting", "Solutions", "Systems", "Trading", "Industries", "Software", "Foods", "Energy", "Partners",
        "Engineering", "Hospitality", "Retail", "Medical", "Marine", "Construction", "Group", "Digital", "Services", "Supplies",
    ];

    public static readonly string[] Websites = ["www", "www", "www", ""];

    // ---- Opportunities, lead companies, activities ----

    public static readonly string[] OpportunityTopics =
    [
        "ERP implementation", "Εγκατάσταση ERP", "Annual support contract", "Ετήσια σύμβαση υποστήριξης", "Licence renewal",
        "Ανανέωση αδειών χρήσης", "Website redesign", "Ανασχεδιασμός ιστοσελίδας", "Cloud migration", "Μετάβαση σε cloud",
        "Equipment supply", "Προμήθεια εξοπλισμού", "Maintenance 2026", "Συντήρηση 2026", "Training programme",
        "Πρόγραμμα εκπαίδευσης", "Fleet management", "Διαχείριση στόλου", "Security audit", "Έλεγχος ασφάλειας",
        "Framework agreement", "Συμφωνία πλαίσιο", "Pilot project", "Πιλοτικό έργο", "POS rollout", "Εγκατάσταση POS",
    ];

    public static readonly string[] CallSubjects =
    [
        "Follow-up call about the offer", "Κλήση για παρακολούθηση προσφοράς", "Intro call", "Κλήση γνωριμίας",
        "Renewal discussion", "Συζήτηση ανανέωσης συμβολαίου", "Payment reminder", "Υπενθύμιση πληρωμής",
        "Call to schedule a demo", "Κλήση για προγραμματισμό επίδειξης", "Support question", "Ερώτηση τεχνικής υποστήριξης",
    ];

    public static readonly string[] MeetingSubjects =
    [
        "On-site visit", "Επίσκεψη στις εγκαταστάσεις", "Product demo", "Επίδειξη προϊόντος", "Contract review",
        "Έλεγχος σύμβασης", "Quarterly business review", "Τριμηνιαία ανασκόπηση", "Kick-off meeting", "Σύσκεψη έναρξης έργου",
        "Negotiation meeting", "Συνάντηση διαπραγμάτευσης",
    ];

    public static readonly string[] TaskSubjects =
    [
        "Send quotation", "Αποστολή προσφοράς", "Prepare proposal", "Προετοιμασία πρότασης", "Update CRM notes",
        "Ενημέρωση σημειώσεων", "Confirm delivery date", "Επιβεβαίωση ημερομηνίας παράδοσης", "Request signed contract",
        "Αίτημα υπογεγραμμένης σύμβασης", "Send product brochure", "Αποστολή φυλλαδίου", "Check payment status",
        "Έλεγχος εξόφλησης", "Arrange training", "Οργάνωση εκπαίδευσης",
    ];

    public static readonly string[] OutcomeNotes =
    [
        "Customer is interested; will decide next month.", "Ο πελάτης ενδιαφέρεται και θα αποφασίσει τον επόμενο μήνα.",
        "Asked for a revised price.", "Ζήτησε αναθεωρημένη τιμή.", "Left a voicemail, will try again.",
        "Δεν απάντησε, θα ξαναπροσπαθήσω.", "Agreed on the next steps.", "Συμφωνήθηκαν τα επόμενα βήματα.",
        "Waiting for the board's approval.", "Αναμένεται έγκριση από το διοικητικό συμβούλιο.",
        "Sent the documents by email.", "Στάλθηκαν τα έγγραφα με email.",
    ];

    // ---- Text helpers ----

    private static readonly Dictionary<char, string> Latin = new()
    {
        ['α'] = "a",
        ['β'] = "v",
        ['γ'] = "g",
        ['δ'] = "d",
        ['ε'] = "e",
        ['ζ'] = "z",
        ['η'] = "i",
        ['θ'] = "th",
        ['ι'] = "i",
        ['κ'] = "k",
        ['λ'] = "l",
        ['μ'] = "m",
        ['ν'] = "n",
        ['ξ'] = "x",
        ['ο'] = "o",
        ['π'] = "p",
        ['ρ'] = "r",
        ['σ'] = "s",
        ['ς'] = "s",
        ['τ'] = "t",
        ['υ'] = "y",
        ['φ'] = "f",
        ['χ'] = "ch",
        ['ψ'] = "ps",
        ['ω'] = "o",
    };

    /// <summary>Lower-case text without accents: "Αθήνα" and "Αθηνα" give the same key, like the database collation.</summary>
    public static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    /// <summary>Greek (or any accented) text as plain lower-case Latin letters and digits, for email addresses and domains.</summary>
    public static string Slug(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in Fold(text))
        {
            if (Latin.TryGetValue(ch, out var latin))
            {
                sb.Append(latin);
            }
            else if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(ch);
            }
            else if (ch is 'ä') { sb.Append('a'); }
            else if (ch is 'ö') { sb.Append('o'); }
            else if (ch is 'ü') { sb.Append('u'); }
            else if (ch is 'ß') { sb.Append("ss"); }
        }

        return sb.ToString();
    }
}
