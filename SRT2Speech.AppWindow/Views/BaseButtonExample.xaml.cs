using System.Windows;
using System.Windows.Controls;

namespace SRT2Speech.AppWindow.Views;

/// <summary>
/// BaseButtonExample - Demonstrates various uses of the BaseButton control
/// </summary>
public partial class BaseButtonExample : UserControl
{
    public BaseButtonExample()
    {
        InitializeComponent();
        
        // Configure Success buttons
        btnSuccess1.SetSuccessStyle();
        btnSuccess2.SetSuccessStyle();
        btnSuccess3.SetSuccessStyle();
        
        // Configure Warning buttons
        btnWarning1.SetWarningStyle();
        btnWarning2.SetWarningStyle();
        
        // Configure Danger buttons
        btnDanger1.SetDangerStyle();
        btnDanger2.SetDangerStyle();
        
        // Configure Secondary buttons
        btnSecondary1.SetSecondaryStyle();
        btnSecondary2.SetSecondaryStyle();
        
        // Configure Info buttons
        btnInfo1.SetInfoStyle();
        btnInfo2.SetInfoStyle();
    }
}