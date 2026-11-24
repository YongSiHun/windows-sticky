using System.Globalization;
using System.Resources;

namespace StickyNote.Utilities
{
    public static class Loc
    {
        private static readonly ResourceManager Resources = new ResourceManager("StickyNote.Resources.Strings", typeof(Loc).Assembly);
        private static readonly ResourceManager English = new ResourceManager("StickyNote.Resources.English", typeof(Loc).Assembly);
        private static CultureInfo _culture = CultureInfo.GetCultureInfo("ko");
        public static CultureInfo Culture { get { return _culture; } set { _culture = value; } }
        public static string Get(string key) { return (Culture.TwoLetterISOLanguageName == "en" ? English : Resources).GetString(key, CultureInfo.InvariantCulture) ?? key; }
    }
}
