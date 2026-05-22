using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using JulesClient.Models;

namespace JulesClient.Services;

public class ChatActivityTemplateSelector : DataTemplateSelector
{
    public DataTemplate DefaultMessageTemplate { get; set; } = null!;
    public DataTemplate CodeReviewTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        if (item is Activity activity)
        {
            // Switch styles entirely based on whether it is a Code Review
            return activity.IsReview ? CodeReviewTemplate : DefaultMessageTemplate;
        }
        return DefaultMessageTemplate;
    }
}
