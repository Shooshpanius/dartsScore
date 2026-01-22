using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace dartsScore.Views
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Code-behind для окна MainWindow. Содержит визуальную логику, связанную с отрисовкой доски
        /// и некоторыми взаимодействиями с ViewModel (подскролл, выбор элементов, простые обработчики).
        /// Основная бизнес-логика приложения реализована в <see cref="ViewModels.MainWindowViewModel"/>.
        /// </summary>

        public MainWindow()
        {
            InitializeComponent();

            // draw initial board
            // Подписываемся на событие изменения размера окна — при изменении размера перерисовываем мишень
            SizeChanged += (s, e) => DrawBoard();
            DartsCanvas.PropertyChanged += (s, e) =>
            {
                if (e.Property == Canvas.BoundsProperty)
                    DrawBoard();
            };

            // Обработчик кликов по канвасу (имитация броска дротика)
            DartsCanvas.PointerPressed += DartsCanvas_PointerPressed;

            // no separate header/results scroll sync needed when using combined ScrollViewer

            // No explicit wiring required; event handlers defined in XAML will be bound automatically.
            // Ensure scroll viewer reference exists
            this.AttachedToVisualTree += (s, e) =>
            {
                // nothing here, just ensure control is loaded
            };
        }

        // Undo button handler
        private void UndoButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var vm = this.DataContext as ViewModels.MainWindowViewModel;
            if (vm == null || _lastThrow == null)
                return;

            vm.UndoThrow(_lastThrow.Value.PrevIndex, _lastThrow.Value.PrevThrowsLeft, _lastThrow.Value.PrevRoundIndex, _lastThrow.Value.PrevThrowsThisRound, _lastThrow.Value.Player, _lastThrow.Value.Points);
            _lastThrow = null;
        }

        private void NextButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var vm = this.DataContext as ViewModels.MainWindowViewModel;
            if (vm == null) return;
            vm.AdvanceTurnManually();
        }

        private void RemoveParticipant_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var p = btn.CommandParameter as ViewModels.MainWindowViewModel.PlayerInfo;
            var vm = this.DataContext as ViewModels.MainWindowViewModel;
            if (vm == null || p == null) return;
            vm.RemoveParticipant(p);
        }

        private double NormalizeAngle(double a)
        {
            // normalize to [-180,180]
            a = (a + 360) % 360;
            if (a > 180) a -= 360;
            return a;
        }

        private void DrawBoard()
        {
            if (DartsCanvas == null)
                return;
            DartsCanvas.Children.Clear();

            double w = DartsCanvas.Bounds.Width > 0 ? DartsCanvas.Bounds.Width : 360;
            double h = DartsCanvas.Bounds.Height > 0 ? DartsCanvas.Bounds.Height : 360;
            double cx = w / 2.0;
            double cy = h / 2.0;
            double radius = Math.Min(w, h) / 2.0;

            // Radii for parts (fractions chosen to look similar to a standard board)
            double innerBullR = radius * 0.06; // 50
            double outerBullR = radius * 0.12; // 25
            double tripleInner = radius * 0.48;
            double tripleOuter = radius * 0.55;
            double doubleInner = radius * 0.88;
            double doubleOuter = radius * 0.99;

            int[] sectorNumbers = new int[] { 20,1,18,4,13,6,10,15,2,17,3,19,7,16,8,11,14,9,12,5 };

            // Draw outer background circle (board base)
            var baseCircle = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = Brushes.DarkSlateGray,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            Canvas.SetLeft(baseCircle, cx - radius);
            Canvas.SetTop(baseCircle, cy - radius);
            DartsCanvas.Children.Add(baseCircle);

            // Helper to compute point on circle
            Point PointOn(double rad, double angleDeg)
            {
                double a = (Math.PI / 180.0) * angleDeg;
                return new Point(cx + Math.Cos(a) * rad, cy + Math.Sin(a) * rad);
            }

            // Draw 20 sector segments: inner single, triple, outer single, double
            for (int i = 0; i < 20; i++)
            {
                // shift start so sector center aligns: sector 0 (20) center at -90 (top)
                double startAngle = -99 + i * 18.0;
                double endAngle = startAngle + 18.0;
                double midAngle = startAngle + 9.0; // will be -90 + i*18

                // main single color alternation
                IBrush mainColor = (i % 2 == 0) ? Brushes.Black : Brushes.WhiteSmoke;

                // inner single (between outerBullR and tripleInner)
                var innerSingle = CreateRingSegment(cx, cy, outerBullR, tripleInner, startAngle, 18.0, mainColor);
                DartsCanvas.Children.Add(innerSingle);

                // triple ring (colored red/green alternating)
                IBrush tg = (i % 2 == 0) ? Brushes.DarkRed : Brushes.DarkGreen;
                var tripleSeg = CreateRingSegment(cx, cy, tripleInner, tripleOuter, startAngle, 18.0, tg);
                DartsCanvas.Children.Add(tripleSeg);

                // outer single (between tripleOuter and doubleInner)
                var outerSingle = CreateRingSegment(cx, cy, tripleOuter, doubleInner, startAngle, 18.0, mainColor);
                DartsCanvas.Children.Add(outerSingle);

                // double ring
                IBrush dg = (i % 2 == 0) ? Brushes.DarkRed : Brushes.DarkGreen;
                var doubleSeg = CreateRingSegment(cx, cy, doubleInner, doubleOuter, startAngle, 18.0, dg);
                DartsCanvas.Children.Add(doubleSeg);

                // sector separating line
                var p1 = PointOn(doubleOuter, startAngle);
                var line = new Line { StartPoint = new Point(cx, cy), EndPoint = p1, Stroke = Brushes.Black, StrokeThickness = 1 };
                DartsCanvas.Children.Add(line);

                // sector number
                var numPoint = PointOn(radius * 1.05, midAngle);
                var txt = new TextBlock
                {
                    Text = sectorNumbers[i].ToString(),
                    Foreground = Brushes.Black,
                    FontSize = Math.Max(12, radius * 0.06),
                    FontWeight = FontWeight.Bold
                };
                // center the text roughly
                txt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(txt, numPoint.X - txt.DesiredSize.Width / 2);
                Canvas.SetTop(txt, numPoint.Y - txt.DesiredSize.Height / 2);
                DartsCanvas.Children.Add(txt);
            }

            // Draw bulls
            var outerBull = new Ellipse { Width = outerBullR * 2, Height = outerBullR * 2, Fill = Brushes.Green, Stroke = Brushes.Black, StrokeThickness = 1 };
            Canvas.SetLeft(outerBull, cx - outerBullR);
            Canvas.SetTop(outerBull, cy - outerBullR);
            DartsCanvas.Children.Add(outerBull);

            var innerBull = new Ellipse { Width = innerBullR * 2, Height = innerBullR * 2, Fill = Brushes.Red, Stroke = Brushes.Black, StrokeThickness = 1 };
            Canvas.SetLeft(innerBull, cx - innerBullR);
            Canvas.SetTop(innerBull, cy - innerBullR);
            DartsCanvas.Children.Add(innerBull);
        }

        private Path CreateRingSegment(double cx, double cy, double innerR, double outerR, double startAngleDeg, double sweepDeg, IBrush fill)
        {
            // Use StreamGeometry to draw an annular sector (outer arc -> inner arc)
            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                double startRad = (Math.PI / 180.0) * startAngleDeg;
                double endRad = (Math.PI / 180.0) * (startAngleDeg + sweepDeg);

                Point p1 = new Point(cx + Math.Cos(startRad) * outerR, cy + Math.Sin(startRad) * outerR);
                Point p2 = new Point(cx + Math.Cos(endRad) * outerR, cy + Math.Sin(endRad) * outerR);
                Point p3 = new Point(cx + Math.Cos(endRad) * innerR, cy + Math.Sin(endRad) * innerR);
                Point p4 = new Point(cx + Math.Cos(startRad) * innerR, cy + Math.Sin(startRad) * innerR);

                ctx.BeginFigure(p1, true);
                // outer arc
                ctx.ArcTo(p2, new Size(outerR, outerR), 0, sweepDeg > 180, SweepDirection.Clockwise);
                // line to inner arc start
                ctx.LineTo(p3);
                // inner arc (reverse)
                ctx.ArcTo(p4, new Size(innerR, innerR), 0, sweepDeg > 180, SweepDirection.CounterClockwise);
                ctx.EndFigure(true);
            }

            var path = new Path
            {
                Data = geom,
                Fill = fill,
                Stroke = Brushes.Black,
                StrokeThickness = 0.5
            };

            return path;
        }

        private void DartsCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var p = e.GetPosition(DartsCanvas);
            double canvasW = DartsCanvas.Bounds.Width > 0 ? DartsCanvas.Bounds.Width : DartsCanvas.Width;
            double canvasH = DartsCanvas.Bounds.Height > 0 ? DartsCanvas.Bounds.Height : DartsCanvas.Height;
            double cx = canvasW / 2.0;
            double cy = canvasH / 2.0;
            double dx = p.X - cx;
            double dy = p.Y - cy;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double radius = Math.Min(canvasW, canvasH) / 2.0;

            // Determine sector by finding the nearest sector center angle to the click angle.
            double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI; // -180..180, 0 = right
            // sector centers used when drawing: centerAngles[i] = -90 + i*18
            int[] sectorNumbers = new int[] { 20,1,18,4,13,6,10,15,2,17,3,19,7,16,8,11,14,9,12,5 };
            double bestDiff = double.MaxValue;
            int sectorIndex = 0;
            for (int i = 0; i < 20; i++)
            {
                double mid = -90.0 + i * 18.0; // center angle in degrees
                // compute minimal angular difference between angle and mid
                double diff = Math.Abs(NormalizeAngle(angle - mid));
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    sectorIndex = i;
                }
            }
            int sector = sectorNumbers[sectorIndex];

            // Determine ring: bullseye (50), outer bull (25), triple, double, single
            int points = 0;
            if (dist <= radius * 0.06)
            {
                // inner bull
                points = 50;
            }
            else if (dist <= radius * 0.12)
            {
                // outer bull
                points = 25;
            }
            else
            {
                // rings relative positions
                double d = dist / radius; // 0..1
                // triple ring approximately between 0.48 and 0.55
                if (d >= 0.48 && d <= 0.55)
                    points = sector * 3;
                // double ring approximately between 0.88 and 0.96
                else if (d >= 0.88 && d <= 0.99)
                    points = sector * 2;
                else if (d <= 1.0)
                    points = sector;
                else
                    points = 0; // missed board
            }

            // add marker
            var marker = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = points > 0 ? Brushes.Gold : Brushes.Gray,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            Canvas.SetLeft(marker, p.X - 4);
            Canvas.SetTop(marker, p.Y - 4);
            DartsCanvas.Children.Add(marker);

            // update scores in ViewModel (per selected player)
            var vm = this.DataContext as ViewModels.MainWindowViewModel;
            if (vm == null)
            {
                // DataContext not available - cannot apply throw
                return;
            }

            // show sector information under the board (if control present)
            try
            {
                if (this.FindControl<TextBlock>("SectorNumberText") is TextBlock sectorTextBlock)
                {
                    string sectorText;
                    if (points == 50)
                        sectorText = "Bull (50)";
                    else if (points == 25)
                        sectorText = "Bull (25)";
                    else if (points == 0)
                        sectorText = "Miss";
                    else if (points == sector * 3)
                        sectorText = $"{sector} x3";
                    else if (points == sector * 2)
                        sectorText = $"{sector} x2";
                    else
                        sectorText = sector.ToString();

                    sectorTextBlock.Text = sectorText;
                }
            }
            catch { }

            // decide which player to apply throw to:
            // prefer selection in the Participants list (SelectedParticipant),
            // otherwise use active participant, otherwise fall back to SelectedPlayer (from Settings)
            var targetPlayer = vm.SelectedParticipant?.Name;
            if (string.IsNullOrEmpty(targetPlayer))
                targetPlayer = vm.ActiveParticipantName;
            if (string.IsNullOrEmpty(targetPlayer))
                targetPlayer = vm.SelectedPlayer;


            // perform throw and get previous state for undo
            var prev = vm.AddScoreToPlayer(targetPlayer, points);
            // store last throw info on window for undo (including round state)
            _lastThrow = (PrevIndex: prev.PrevIndex, PrevThrowsLeft: prev.PrevThrowsLeft, PrevRoundIndex: prev.PrevRoundIndex, PrevThrowsThisRound: prev.PrevThrowsThisRound, Player: targetPlayer, Points: points);


            // ensure the active participant is selected in the UI list
            try
            {
                if (!string.IsNullOrEmpty(vm.ActiveParticipantName))
                {
                    var list = this.FindControl<ListBox>("ParticipantsList");
                    if (list != null)
                    {
                        var part = System.Linq.Enumerable.FirstOrDefault(vm.Participants, x => x.Name == vm.ActiveParticipantName);
                        if (part != null)
                        {
                            list.SelectedItem = part;
                            // set temporary highlight flag on the participant to trigger UI change
                            part.IsHighlighted = true;
                            // clear highlight after short delay
                            _ = ClearHighlightAsync(part);
                            // scroll round results into view for this participant
                            var scroll = this.FindControl<ScrollViewer>("RoundResultsScroll");
                            if (scroll != null)
                            {
                                // attempt to scroll to vertical offset corresponding to participant index
                                int idx = vm.Participants.IndexOf(part);
                                double itemHeight = 28; // approx per-row height
                                try
                                {
                                    scroll.Offset = new Avalonia.Vector(0, idx * itemHeight);
                                }
                                catch { }
                            }
                            // synchronize headers scroll position with results scroll
                            var headerScroll = this.FindControl<ScrollViewer>("RoundHeadersScroll");
                            if (headerScroll != null && scroll != null)
                            {
                                try { headerScroll.Offset = new Avalonia.Vector(scroll.Offset.X, 0); } catch { }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private (int PrevIndex, int PrevThrowsLeft, int PrevRoundIndex, int PrevThrowsThisRound, string Player, int Points)? _lastThrow;

        

        private async System.Threading.Tasks.Task ClearHighlightAsync(ViewModels.MainWindowViewModel.PlayerInfo part)
        {
            await System.Threading.Tasks.Task.Delay(300);
            part.IsHighlighted = false;
        }

    }
}
