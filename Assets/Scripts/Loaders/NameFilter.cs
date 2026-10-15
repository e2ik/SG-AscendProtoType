using System;
using System.Collections.Generic;
using System.Text;

public static class NameFilter
{
    public const char DefaultMask = '\u2588';

    public static readonly string[] ContainsWords =
    {
        "fuck", "fuk", "fck", "fcuk", "fvck", "phuck", "fuq", "motherf", "mofo",
        "shit", "sht", "bitch", "btch", "biatch", "bytch",
        "cunt", "kunt", "twat", "pussy", "whore", "slut", "sloot", "bastard", "wank", "jerkoff",
        "bollocks", "bugger", "piss", "asshole", "arsehole", "dumbass", "jackass", "smartass", "butthole",
        "penis", "vagina", "testicle", "scrotum", "ballsack", "nutsack", "boob", "tits", "titty", "titties",
        "nipple", "clit", "coochie", "schlong",
        "porn", "sex", "horny", "nude", "naked", "orgasm", "erotic", "fetish", "kinky", "bdsm", "bondage",
        "dildo", "vibrator", "blowjob", "handjob", "rimjob", "boner", "jizz", "cumshot", "semen",
        "milf", "dilf", "hentai", "onlyfans", "stripper", "hooker", "thot", "cuck",
        "rapist", "molest", "pedo", "paedo", "incest", "bestiality",
        "nigger", "nigga", "faggot", "retard", "tranny", "dyke", "chink", "kike", "gook",
        "wetback", "beaner", "raghead", "towelhead", "spastic", "spaz",
        "hitler", "heil", "kkk", "swastika", "jihad", "terrorist",
        "killyourself", "suicide", "selfharm", "cutmyself",
        "cocaine", "heroin", "stoner", "druggie",
        "deeznuts", "ligma", "sugma"
    };

    public static readonly string[] WholeWords =
    {
        "ass", "arse", "hoe", "hoes", "sus", "coon", "cum", "paki", "dick", "dicks", "cock", "cocks",
        "spic", "nazi", "nazis", "rape", "tit", "fag", "fags", "anal", "anus", "gay", "homo", "niga",
        "poo", "poop", "fart", "butt", "bum", "balls", "nuts", "nut", "pee", "wee",
        "knob", "nob", "dong", "wiener", "weiner", "pecker", "boobs", "jugs", "vag", "puss", "thicc",
        "slag", "skank", "prick", "simp", "damn", "hell", "crap",
        "weed", "crack", "meth", "bofa", "kys", "kms",
        "69", "420", "666"
    };

    public static bool IsBlocked(string text) => IsBlocked(text, ContainsWords, WholeWords);

    public static string Censor(string text, char mask = DefaultMask) => Censor(text, ContainsWords, WholeWords, mask);

    public static bool IsBlocked(string text, IEnumerable<string> containsWords, IEnumerable<string> wholeWords) =>
        !string.IsNullOrEmpty(text) && Censor(text, containsWords, wholeWords) != text;

    public static string Censor(string text, IEnumerable<string> containsWords, IEnumerable<string> wholeWords, char mask = DefaultMask)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var hide = new bool[text.Length];
        var normalised = new StringBuilder(text.Length);
        var map = new List<int>(text.Length);

        for (int i = 0; i < text.Length; i++)
        {
            char c = MapChar(char.ToLowerInvariant(text[i]));
            if (!char.IsLetter(c)) continue;

            normalised.Append(c);
            map.Add(i);
        }

        string cleaned = normalised.ToString();

        if (containsWords != null)
        {
            foreach (var word in containsWords)
            {
                string target = Normalise(word);
                if (target.Length == 0) continue;

                int index = cleaned.IndexOf(target, StringComparison.Ordinal);
                while (index >= 0)
                {
                    Mark(hide, map[index], map[index + target.Length - 1]);
                    index = cleaned.IndexOf(target, index + 1, StringComparison.Ordinal);
                }
            }
        }

        if (wholeWords != null)
        {
            var tokens = SplitWords(text);
            string wholeText = text.Trim().ToLowerInvariant();

            foreach (var word in wholeWords)
            {
                if (string.IsNullOrWhiteSpace(word)) continue;

                string raw = word.Trim().ToLowerInvariant();
                string target = Normalise(word);

                if (wholeText == raw || (target.Length > 0 && cleaned == target))
                {
                    Mark(hide, 0, text.Length - 1);
                    continue;
                }

                foreach (var (start, end) in tokens)
                {
                    string token = text.Substring(start, end - start).ToLowerInvariant();
                    if (token == raw || (target.Length > 0 && Normalise(token) == target))
                        Mark(hide, start, end - 1);
                }
            }
        }

        var result = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
            result.Append(hide[i] && !char.IsWhiteSpace(text[i]) ? mask : text[i]);

        return result.ToString();
    }

    private static void Mark(bool[] hide, int from, int to)
    {
        for (int i = from; i <= to; i++)
            hide[i] = true;
    }

    private static List<(int start, int end)> SplitWords(string text)
    {
        var words = new List<(int, int)>();
        int start = -1;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            bool separator = char.IsWhiteSpace(c) || c == '_' || c == '-' || c == '.';
            bool newWord = i > 0 && char.IsUpper(c) && char.IsLower(text[i - 1]);

            if ((separator || newWord) && start >= 0)
            {
                words.Add((start, i));
                start = -1;
            }

            if (!separator && start < 0)
                start = i;
        }

        if (start >= 0)
            words.Add((start, text.Length));

        return words;
    }

    private static string Normalise(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var builder = new StringBuilder(text.Length);
        foreach (char raw in text.ToLowerInvariant())
        {
            char c = MapChar(raw);
            if (char.IsLetter(c))
                builder.Append(c);
        }
        return builder.ToString();
    }

    private static char MapChar(char c) => c switch
    {
        '0' => 'o',
        '1' => 'i',
        '!' => 'i',
        '3' => 'e',
        '4' => 'a',
        '@' => 'a',
        '5' => 's',
        '$' => 's',
        '7' => 't',
        _ => c
    };
}