using System.Runtime.CompilerServices;

namespace Plugin.Maui.CircularProgressBar;

public class CircularProgressBar : GraphicsView
{
    private readonly IndeterminateBarDrawable _indeterminateDrawable = new();
    private CancellationTokenSource _indeterminateCts;
    private readonly ProgressBarDrawable _progressDrawable = new();

    private double _prevProgress;

    public CircularProgressBar()
    {
        _indeterminateDrawable.SetBinding(IndeterminateBarDrawable.ColorProperty, new Binding(ColorProperty.PropertyName, source: this));
        _indeterminateDrawable.SetBinding(IndeterminateBarDrawable.ThicknessProperty, new Binding(ThicknessProperty.PropertyName, source: this));
        _progressDrawable.SetBinding(IndeterminateBarDrawable.ColorProperty, new Binding(ColorProperty.PropertyName, source: this));
        _progressDrawable.SetBinding(IndeterminateBarDrawable.ThicknessProperty, new Binding(ThicknessProperty.PropertyName, source: this));

        WidthRequest = 50d;
        HeightRequest = 50d;

        this.Drawable = IsIndeterminate ? _indeterminateDrawable : _progressDrawable;
        //this.Drawable = _indeterminateDrawable;
    }

    #region Color
    public Color Color
    {
        get { return (Color)GetValue(ColorProperty); }
        set { SetValue(ColorProperty, value); }
    }

    public static readonly BindableProperty ColorProperty =
        BindableProperty.Create(
            nameof(Color),
            typeof(Color),
            typeof(CircularProgressBar),
            Colors.Blue
            );
    #endregion

    #region Progress
    public double Progress
    {
        get { return (double)GetValue(ProgressProperty); }
        set { SetValue(ProgressProperty, value); }
    }

    public static readonly BindableProperty ProgressProperty =
        BindableProperty.Create(
            nameof(Progress),
            typeof(double),
            typeof(CircularProgressBar)
            );
    #endregion

    #region Thickness
    public float Thickness
    {
        get { return (float)GetValue(ThicknessProperty); }
        set { SetValue(ThicknessProperty, value); }
    }

    public static readonly BindableProperty ThicknessProperty =
        BindableProperty.Create(
            nameof(Thickness),
            typeof(float),
            typeof(CircularProgressBar),
            4f
            );
    #endregion

    #region Smooth
    public bool Smooth
    {
        get { return (bool)GetValue(SmoothProperty); }
        set { SetValue(SmoothProperty, value); }
    }

    public static readonly BindableProperty SmoothProperty =
        BindableProperty.Create(
            nameof(Smooth),
            typeof(bool),
            typeof(CircularProgressBar),
            false
            );
    #endregion

    #region IsIndeterminate
    public bool IsIndeterminate
    {
        get { return (bool)GetValue(IsIndeterminateProperty); }
        set { SetValue(IsIndeterminateProperty, value); }
    }

    public static readonly BindableProperty IsIndeterminateProperty =
        BindableProperty.Create(
            nameof(IsIndeterminate),
            typeof(bool),
            typeof(CircularProgressBar),
            false
            );
    #endregion

    protected override void OnPropertyChanging([CallerMemberName] string propertyName = null)
    {
        base.OnPropertyChanging(propertyName);

        if (propertyName == ProgressProperty.PropertyName)
        {
            _prevProgress = Progress;
        }
    }

    protected override void OnPropertyChanged(string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == IsIndeterminateProperty.PropertyName && IsIndeterminate)
        {
            StopProgress();

            this.Drawable = _indeterminateDrawable;

            StartIndeterminate();
        }

        if (IsIndeterminate) return;

        if (propertyName == IsIndeterminateProperty.PropertyName && !IsIndeterminate)
        {
            StopIndeterminate();
        }

        this.Drawable = _progressDrawable;

        if (propertyName == ProgressProperty.PropertyName && Smooth && Progress < 1)
        {
            StopProgress();

            StartProgress();
        }
        else
        {
            _progressDrawable.End = Progress;
            Invalidate();
        }
    }

    private void StopProgress()
    {
        this.AbortAnimation("progress");
    }

    private void StartProgress()
    {
        this.Animate("progress", (p) =>
        {
            _progressDrawable.End = p;
            Invalidate();
        }, start: _prevProgress, end: Progress, length: 200, easing: Easing.SinOut);
    }

    private void StopIndeterminate()
    {
        this.AbortAnimation("rotation");
        this.AbortAnimation("indeterminateForward");
        this.AbortAnimation("indeterminateBackward");

        this.Rotation = 0;

        try
        {
            _indeterminateCts?.Cancel();
        }
        catch { }

        _indeterminateCts = null;
    }

    private void StartIndeterminate()
    {
        _indeterminateCts = new();

        this.Animate("rotation", de =>
        {
            this.Rotation = de;
        }, 0, 359, length: 2000, repeat: () => true);

        StartIndeterminate(_indeterminateCts.Token, 0, 0);
    }

    private async void StartIndeterminate(CancellationToken token, double start, double end)
    {
        start = end;
        end = start - 0.1;
        if (end < 0) end += 1d;

        if (token.IsCancellationRequested) return;

        this.Animate("indeterminateForward", (de) =>
        {
            _indeterminateDrawable.Start = start;
            _indeterminateDrawable.End = de - 1d + 0.05;
            _indeterminateDrawable.IsClockwise = true;
            Invalidate();
        }, start, end < start ? end + 1d : end, length: 750, easing: Easing.SinInOut, finished: async (v, c) =>
        {
            if (token.IsCancellationRequested || c) return;

            await Task.Delay(250);

            this.Animate("indeterminateBackward", (de) =>
            {
                _indeterminateDrawable.Start = start - 0.05;
                _indeterminateDrawable.End = de - 1d;
                _indeterminateDrawable.IsClockwise = false;
                Invalidate();
            }, start, end < start ? end + 1d : end, length: 750, easing: Easing.SinInOut, finished: async (v, c) =>
            {
                if (c) return;

                await Task.Delay(250);

                StartIndeterminate(token, start, end);
            });
        });
    }
}

public class IndeterminateBarDrawable : BindableObject, IDrawable
{
    public double Start { get; set; }
    public double End { get; set; }
    public bool IsClockwise { get; set; }

    public virtual void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.StrokeLineCap = LineCap.Round;

        var width = dirtyRect.Width - Thickness;
        var height = dirtyRect.Height - Thickness;

        var x = Thickness / 2f;
        var y = x;

        canvas.StrokeColor = Color.WithAlpha(0.2f);
        canvas.StrokeSize = Thickness * 0.9f;
        canvas.DrawEllipse(x, y, width, height);

        canvas.StrokeColor = Color;
        canvas.StrokeSize = Thickness;

        var start = 360f - (float)Start * 360f;
        var end = 360f - (float)End * 360f;

        canvas.DrawArc(x, y, width, height, start, end, IsClockwise, false);
    }

    #region Color
    public Color Color
    {
        get { return (Color)GetValue(ColorProperty); }
        set { SetValue(ColorProperty, value); }
    }

    public static readonly BindableProperty ColorProperty =
        BindableProperty.Create(
            nameof(Color),
            typeof(Color),
            typeof(IndeterminateBarDrawable),
            Colors.Blue
            );
    #endregion

    #region Thickness
    public float Thickness
    {
        get { return (float)GetValue(ThicknessProperty); }
        set { SetValue(ThicknessProperty, value); }
    }

    public static readonly BindableProperty ThicknessProperty =
        BindableProperty.Create(
            nameof(Thickness),
            typeof(float),
            typeof(IndeterminateBarDrawable),
            4f
            );
    #endregion
}

public class ProgressBarDrawable : IndeterminateBarDrawable, IDrawable
{
    public ProgressBarDrawable()
    {
        Start = 0.25;
    }

    public override void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.StrokeLineCap = LineCap.Round;

        var width = dirtyRect.Width - Thickness;
        var height = dirtyRect.Height - Thickness;

        var x = Thickness / 2f;
        var y = x;

        End = End < 0d ? 0d : (End > 1d ? 1d : End);

        if (End < 1)
        {
            canvas.StrokeColor = Color.WithAlpha(0.2f);
            canvas.StrokeSize = Thickness * 0.9f;
            canvas.DrawEllipse(x, y, width, height);

            canvas.StrokeColor = Color;
            canvas.StrokeSize = Thickness;
            canvas.DrawArc(x, y, width, height, (float)Start * 360f, (float)GetAngle(), true, false);
        }
        else
        {
            canvas.StrokeColor = Color;
            canvas.StrokeSize = Thickness;
            canvas.DrawEllipse(x, y, width, height);
        }
    }

    private float GetAngle()
    {
        var angle = (float)(Start - End) * 360f;

        return angle < 0f ? 360f + angle : angle;
    }
}