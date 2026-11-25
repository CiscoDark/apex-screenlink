using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Apex.Desktop;
internal static class Motion
{
    public static bool Reduce {get;set;}
    public static bool Enabled => !Reduce && SystemParameters.ClientAreaAnimation;
    public static void Reveal(UIElement element)
    {
        if(!Enabled)return;
        var translate=new TranslateTransform();element.RenderTransform=translate;
        element.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(190)));
        translate.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(9,0,TimeSpan.FromMilliseconds(230)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
    }
    public static void Button(Button button)
    {
        button.MouseEnter+=(_,_)=>{if(Enabled)button.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(1,.88,TimeSpan.FromMilliseconds(110)));};
        button.MouseLeave+=(_,_)=>button.BeginAnimation(UIElement.OpacityProperty,Enabled?new DoubleAnimation(1,TimeSpan.FromMilliseconds(150)):null);
    }
}
