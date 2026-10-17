using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
namespace ExamHelper.App.Controls;
public partial class ThumbnailStrip : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource),typeof(IEnumerable),typeof(ThumbnailStrip),new PropertyMetadata(null,Changed));
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty,value); }
    private INotifyCollectionChanged? _source;
    internal ScrollViewer ScrollViewer => Scroller;
    public ThumbnailStrip()
    {
        InitializeComponent(); Loaded += (_,_) => Attach(); Unloaded += (_,_) => Detach();
    }
    private static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs args)
    {
        var strip = (ThumbnailStrip)d; strip.Detach(); if(strip.Items is not null) strip.Items.ItemsSource = (IEnumerable?)args.NewValue; if(strip.IsLoaded) strip.Attach();
    }
    private void Attach()
    {
        Detach(); _source = ItemsSource as INotifyCollectionChanged; if(_source is not null) _source.CollectionChanged += CollectionChanged;
    }
    private void Detach() { if(_source is not null) _source.CollectionChanged -= CollectionChanged; _source = null; }
    private void CollectionChanged(object? sender,NotifyCollectionChangedEventArgs args)
    {
        if(Window.GetWindow(this) is { } window) DwellBehavior.InvalidateWindow(window);
    }
}