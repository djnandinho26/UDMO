using Microsoft.AspNetCore.Components;
using MudBlazor;
namespace DigitalWorldOnline.Admin.Shared
{
    public partial class ConfirmDialog
    {
        [CascadingParameter] 
        IDialogReference MudDialog { get; set; } = null!;

        [Parameter] 
        public string ContentText { get; set; } = string.Empty;

        [Parameter]
        public Color Color { get; set; } = Color.Error;

        void Submit() => MudDialog.Close(DialogResult.Ok(true));
        void Cancel() => MudDialog.Close(DialogResult.Cancel());
    }
}