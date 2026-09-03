using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using JulesClient.Models;

namespace JulesClient.Services;

public class ChatActivityTemplateSelector : DataTemplateSelector
{
    public DataTemplate DefaultMessageTemplate { get; set; } = null!;
    public DataTemplate CodeReviewTemplate { get; set; } = null!;
    public DataTemplate SystemEventTemplate { get; set; } = null!;
    public DataTemplate ChangeNoteTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        if (item is Activity activity)
        {
            if (activity.IsChangeNote && ChangeNoteTemplate != null) return ChangeNoteTemplate;
            if (activity.IsSystemEvent && SystemEventTemplate != null) return SystemEventTemplate;
            if (activity.IsReview) return CodeReviewTemplate;
            return DefaultMessageTemplate;
        }
        return DefaultMessageTemplate;
    }
}
