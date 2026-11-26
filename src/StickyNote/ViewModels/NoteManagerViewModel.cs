using System.ComponentModel;
using System.Windows.Data;
using StickyNote.Models;
using StickyNote.Services;

namespace StickyNote.ViewModels
{
    public sealed class NoteManagerViewModel : Observable
    {
        public ICollectionView Notes { get; private set; }
        private string search = "";
        public string Search { get { return search; } set { if (Set(ref search, value)) Notes.Refresh(); } }
        public Note Selected { get; set; }
        // INIT: wire owned state and events.
        public NoteManagerViewModel(NoteService service)
        {
            Notes = new ListCollectionView(service.Notes);
            Notes.Filter = item => { var n = (Note)item; return string.IsNullOrWhiteSpace(search) || n.title.IndexOf(search, System.StringComparison.CurrentCultureIgnoreCase) >= 0 || n.content.IndexOf(search, System.StringComparison.CurrentCultureIgnoreCase) >= 0; };
        }
    }
}
