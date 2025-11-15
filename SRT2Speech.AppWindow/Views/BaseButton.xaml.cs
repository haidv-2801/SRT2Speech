using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SRT2Speech.AppWindow.Views;

/// <summary>
/// BaseButton - Custom reusable button control with various style variants
/// </summary>
public partial class BaseButton : UserControl
{
    public BaseButton()
    {
        InitializeComponent();
        
        // Set default values
        ButtonWidth = 120;
        ButtonHeight = 32;
        CornerRadius = new CornerRadius(4);
        ButtonPadding = new Thickness(10, 5, 10, 5);
        IconSize = 16;
        IconVisibility = Visibility.Collapsed;
        IsButtonEnabled = true;
        
        // Set default Primary button style
        SetPrimaryStyle();
    }

    #region Dependency Properties

    // Text Property
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(BaseButton), 
            new PropertyMetadata(string.Empty));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // Icon Property
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(BaseButton), 
            new PropertyMetadata(string.Empty, OnIconChanged));

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BaseButton button)
        {
            button.IconVisibility = string.IsNullOrEmpty(e.NewValue as string) 
                ? Visibility.Collapsed 
                : Visibility.Visible;
        }
    }

    // IconSize Property
    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(BaseButton), 
            new PropertyMetadata(16.0));

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    // IconVisibility Property
    public static readonly DependencyProperty IconVisibilityProperty =
        DependencyProperty.Register(nameof(IconVisibility), typeof(Visibility), typeof(BaseButton), 
            new PropertyMetadata(Visibility.Collapsed));

    public Visibility IconVisibility
    {
        get => (Visibility)GetValue(IconVisibilityProperty);
        set => SetValue(IconVisibilityProperty, value);
    }

    // Command Property
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(BaseButton), 
            new PropertyMetadata(null));

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    // CommandParameter Property
    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(BaseButton), 
            new PropertyMetadata(null));

    public object CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    // Tooltip Property
    public static readonly DependencyProperty TooltipProperty =
        DependencyProperty.Register(nameof(Tooltip), typeof(string), typeof(BaseButton), 
            new PropertyMetadata(string.Empty));

    public string Tooltip
    {
        get => (string)GetValue(TooltipProperty);
        set => SetValue(TooltipProperty, value);
    }

    // IsButtonEnabled Property
    public static readonly DependencyProperty IsButtonEnabledProperty =
        DependencyProperty.Register(nameof(IsButtonEnabled), typeof(bool), typeof(BaseButton), 
            new PropertyMetadata(true));

    public bool IsButtonEnabled
    {
        get => (bool)GetValue(IsButtonEnabledProperty);
        set => SetValue(IsButtonEnabledProperty, value);
    }

    // ButtonWidth Property
    public static readonly DependencyProperty ButtonWidthProperty =
        DependencyProperty.Register(nameof(ButtonWidth), typeof(double), typeof(BaseButton), 
            new PropertyMetadata(120.0));

    public double ButtonWidth
    {
        get => (double)GetValue(ButtonWidthProperty);
        set => SetValue(ButtonWidthProperty, value);
    }

    // ButtonHeight Property
    public static readonly DependencyProperty ButtonHeightProperty =
        DependencyProperty.Register(nameof(ButtonHeight), typeof(double), typeof(BaseButton), 
            new PropertyMetadata(32.0));

    public double ButtonHeight
    {
        get => (double)GetValue(ButtonHeightProperty);
        set => SetValue(ButtonHeightProperty, value);
    }

    // CornerRadius Property
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(BaseButton), 
            new PropertyMetadata(new CornerRadius(4)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    // ButtonPadding Property
    public static readonly DependencyProperty ButtonPaddingProperty =
        DependencyProperty.Register(nameof(ButtonPadding), typeof(Thickness), typeof(BaseButton), 
            new PropertyMetadata(new Thickness(10, 5, 10, 5)));

    public Thickness ButtonPadding
    {
        get => (Thickness)GetValue(ButtonPaddingProperty);
        set => SetValue(ButtonPaddingProperty, value);
    }

    // Background Colors
    public static readonly DependencyProperty ButtonBackgroundProperty =
        DependencyProperty.Register(nameof(ButtonBackground), typeof(Brush), typeof(BaseButton), 
            new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007ACC"))));

    public Brush ButtonBackground
    {
        get => (Brush)GetValue(ButtonBackgroundProperty);
        set => SetValue(ButtonBackgroundProperty, value);
    }

    public static readonly DependencyProperty HoverBackgroundProperty =
        DependencyProperty.Register(nameof(HoverBackground), typeof(Brush), typeof(BaseButton), 
            new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#005A9E"))));

    public Brush HoverBackground
    {
        get => (Brush)GetValue(HoverBackgroundProperty);
        set => SetValue(HoverBackgroundProperty, value);
    }

    public static readonly DependencyProperty PressedBackgroundProperty =
        DependencyProperty.Register(nameof(PressedBackground), typeof(Brush), typeof(BaseButton), 
            new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#004578"))));

    public Brush PressedBackground
    {
        get => (Brush)GetValue(PressedBackgroundProperty);
        set => SetValue(PressedBackgroundProperty, value);
    }

    public static readonly DependencyProperty DisabledBackgroundProperty =
        DependencyProperty.Register(nameof(DisabledBackground), typeof(Brush), typeof(BaseButton), 
            new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"))));

    public Brush DisabledBackground
    {
        get => (Brush)GetValue(DisabledBackgroundProperty);
        set => SetValue(DisabledBackgroundProperty, value);
    }

    // Foreground Colors
    public static readonly DependencyProperty ButtonForegroundProperty =
        DependencyProperty.Register(nameof(ButtonForeground), typeof(Brush), typeof(BaseButton), 
            new PropertyMetadata(Brushes.White));

    public Brush ButtonForeground
    {
        get => (Brush)GetValue(ButtonForegroundProperty);
        set => SetValue(ButtonForegroundProperty, value);
    }

    public static readonly DependencyProperty DisabledForegroundProperty =
        DependencyProperty.Register(nameof(DisabledForeground), typeof(Brush), typeof(BaseButton), 
            new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"))));

    public Brush DisabledForeground
    {
        get => (Brush)GetValue(DisabledForegroundProperty);
        set => SetValue(DisabledForegroundProperty, value);
    }

    #endregion

    #region Style Methods

    /// <summary>
    /// Set Primary button style (Blue)
    /// </summary>
    public void SetPrimaryStyle()
    {
        ButtonBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007ACC"));
        HoverBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#005A9E"));
        PressedBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#004578"));
        ButtonForeground = Brushes.White;
    }

    /// <summary>
    /// Set Success button style (Green)
    /// </summary>
    public void SetSuccessStyle()
    {
        ButtonBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745"));
        HoverBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#218838"));
        PressedBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E7E34"));
        ButtonForeground = Brushes.White;
    }

    /// <summary>
    /// Set Warning button style (Yellow)
    /// </summary>
    public void SetWarningStyle()
    {
        ButtonBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC107"));
        HoverBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0A800"));
        PressedBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D39E00"));
        ButtonForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#212529"));
    }

    /// <summary>
    /// Set Danger button style (Red)
    /// </summary>
    public void SetDangerStyle()
    {
        ButtonBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"));
        HoverBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C82333"));
        PressedBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BD2130"));
        ButtonForeground = Brushes.White;
    }

    /// <summary>
    /// Set Secondary button style (Gray)
    /// </summary>
    public void SetSecondaryStyle()
    {
        ButtonBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D"));
        HoverBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5A6268"));
        PressedBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#545B62"));
        ButtonForeground = Brushes.White;
    }

    /// <summary>
    /// Set Info button style (Light Blue)
    /// </summary>
    public void SetInfoStyle()
    {
        ButtonBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#17A2B8"));
        HoverBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#138496"));
        PressedBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#117A8B"));
        ButtonForeground = Brushes.White;
    }

    /// <summary>
    /// Set custom colors for the button
    /// </summary>
    public void SetCustomStyle(string background, string hover, string pressed, string foreground = "#FFFFFF")
    {
        ButtonBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(background));
        HoverBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hover));
        PressedBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(pressed));
        ButtonForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(foreground));
    }

    #endregion
}