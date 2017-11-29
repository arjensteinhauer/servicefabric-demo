namespace ilionx.MicroServices.ClientApp
{
    using Microsoft.ServiceFabric.Actors;
    using Microsoft.ServiceFabric.Actors.Client;
    using Microsoft.ServiceFabric.Services.Client;
    using Microsoft.ServiceFabric.Services.Remoting.Client;
    using ShapeActor.Interfaces;
    using ShapeList.Interfaces;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Shapes;
    using System.Windows.Threading;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        internal static bool isClosing = false;
        private DispatcherTimer refreshTimer = null;
        private static object shapesLock = new object();
        private static object timerLock = new object();
        private bool busyRefresh = false;

        /// <summary>
        /// ShapeActor service url.
        /// </summary>
        private static Uri shapeActorServiceUri = new Uri("fabric:/ilionx.MicroServices.DemoServices/ShapeActorService");

        /// <summary>
        /// ShapeList service url.
        /// </summary>
        private static Uri shapeListServiceUri = new Uri("fabric:/ilionx.MicroServices.DemoServices/ShapeListService");

        /// <summary>
        /// ShapeList service.
        /// </summary>
        private IShapeList shapeListService;

        /// <summary>
        /// All shapes.
        /// </summary>
        private Dictionary<Guid, ShapeState> shapes;

        /// <summary>
        /// Gets the client ID from the app settings.
        /// </summary>
        private Guid ClientId
        {
            get
            {
                return Guid.Parse(ConfigurationManager.AppSettings["clientId"]);
            }
        }

        /// <summary>
        /// Shape state.
        /// </summary>
        private class ShapeState
        {
            /// <summary>
            /// The shape.
            /// </summary>
            public IShapeActor Shape { get; private set; }

            /// <summary>
            /// The shape event handler.
            /// </summary>
            public ShapeEventsHandler ShapeEventHandler { get; private set; }

            /// <summary>
            /// Default constructor.
            /// </summary>
            /// <param name="shape">The shape.</param>
            /// <param name="shapeEventHandler">The shape event handler.</param>
            public ShapeState(IShapeActor shape, ShapeEventsHandler shapeEventHandler)
            {
                this.Shape = shape;
                this.ShapeEventHandler = shapeEventHandler;
            }

            /// <summary>
            /// Unsubscribe to the shape event handler.
            /// </summary>
            /// <returns>Async task.</returns>
            public async Task UnsubscribeAsync()
            {
                await this.Shape.UnsubscribeAsync(this.ShapeEventHandler);
            }
        }

        /// <summary>
        /// Handler for ShapeEvents.
        /// </summary>
        private class ShapeEventsHandler : IShapeEvents
        {
            private Canvas shapesCanvas;
            public Rectangle UiShape { get; private set; }

            /// <summary>
            /// Default constructor.
            /// </summary>
            /// <param name="shapesCanvas">The shaped canvas to draw shapes on.</param>
            /// <param name="uiShape">The UI shape element.</param>
            public ShapeEventsHandler(Canvas shapesCanvas, Rectangle uiShape)
            {
                this.shapesCanvas = shapesCanvas;
                this.UiShape = uiShape;
            }

            /// <summary>
            /// ShapeChanged event. A new shape position is sent.
            /// </summary>
            /// <param name="shape">The new shape position.</param>
            public async void ShapeChanged(Models.Shape shape)
            {
                if (isClosing)
                {
                    return;
                }

                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    Canvas.SetTop(UiShape, shape.Y);
                    Canvas.SetLeft(UiShape, shape.X);
                    ((RotateTransform)UiShape.RenderTransform).Angle = shape.Angle;

                    if (!shapesCanvas.Children.Contains(UiShape))
                    {
                        shapesCanvas.Children.Add(UiShape);
                    }
                });
            }
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // create a dictionary for holding the shapes
            shapes = new Dictionary<Guid, ShapeState>();

            // create a service proxy to the service list service
            shapeListService = ServiceProxy.Create<IShapeList>(shapeListServiceUri, partitionKey: new ServicePartitionKey(0));

            // need a timer to validate the callback connection to the shape actors (lost during upgrade/failover)
            refreshTimer = new DispatcherTimer();
            refreshTimer.Interval = TimeSpan.FromMilliseconds(100);
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();
        }

        /// <summary>
        /// Window loaded event. Is sent when the window is activated and loaded.
        /// Restore the shape states from the shape list.
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="e">Event paraeters.</param>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // get the shape list for this client
                var shapeList = await shapeListService.GetShapes(ClientId);

                // restore the shapes
                await Task.WhenAll(shapeList.Select(shapeId => CreateNewShape(shapeId)));
            }
            catch (Exception ex)
            {
                string errorText = GetExceptionMessageText(ex);
                MessageBox.Show(errorText);
            }
        }

        /// <summary>
        /// AddShapeButton click event. Add a new shape to the canvas.
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="e">Event paraeters.</param>
        private async void AddShapeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // create a new shape ID
                Guid shapeId = Guid.NewGuid();

                // create a new shape
                await CreateNewShape(shapeId);

                // add it to the shapelist for this client
                await shapeListService.AddShape(ClientId, shapeId);
            }
            catch (Exception ex)
            {
                string errorText = GetExceptionMessageText(ex);
                MessageBox.Show(errorText);
            }
        }

        /// <summary>
        /// Creates a new shape and adds it to the canvas.
        /// </summary>
        /// <param name="shapeId">The ID of the shape to add.</param>
        /// <returns>Async task.</returns>
        private async Task CreateNewShape(Guid shapeId)
        {
            // create a shape UI element
            RotateTransform rotation = new RotateTransform() { Angle = 0, CenterX = 50, CenterY = 50 };
            Rectangle shape = new Rectangle()
            {
                Height = 100,
                Width = 100,
                Stroke = new SolidColorBrush(Colors.Yellow),
                StrokeThickness = 5,
                RadiusX = 20,
                RadiusY = 20,
                RenderTransform = rotation
            };

            // create a new shape
            ActorId actorId = new ActorId(shapeId);
            IShapeActor shapeActor = ActorProxy.Create<IShapeActor>(actorId, shapeActorServiceUri);
            var currentShape = await shapeActor.GetCurrentPositionAsync();

            // subscribe to all shape change events
            var shapeEventHandler = new ShapeEventsHandler(ShapesCanvas, shape);
            await shapeActor.SubscribeAsync<IShapeEvents>(shapeEventHandler, new TimeSpan(0, 0, 5));

            // save the shape state
            shapes.Add(shapeId, new ShapeState(shapeActor, shapeEventHandler));
        }

        /// <summary>
        /// Window closing event. Is sent when the window is closing.
        /// Unsubscribe to all shape events.
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e"></param>
        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            isClosing = true;

            // stop timer
            if (refreshTimer != null)
            {
                refreshTimer.Stop();
                refreshTimer = null;
            }

            // unsubscribe to all shape change events.
            foreach (Guid shapeId in shapes.Keys)
            {
                await shapes[shapeId].UnsubscribeAsync();
            }
        }

        /// <summary>
        /// Timer tick event to refresh actors.
        /// Needed when upgrading cluster.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            if (busyRefresh) return;

            lock (timerLock)
            {
                busyRefresh = true;
            }

            List<Guid> shapeIds;
            lock (shapesLock)
            {
                shapeIds = shapes.Keys.ToList();
            }

            foreach (Guid shapeId in shapeIds)
            {
                try
                {
                    await shapes[shapeId].Shape.GetCurrentPositionAsync();
                }
                catch (Exception)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (ShapesCanvas.Children.Contains(shapes[shapeId].ShapeEventHandler.UiShape))
                        {
                            ShapesCanvas.Children.Remove(shapes[shapeId].ShapeEventHandler.UiShape);
                        }
                    });

                    lock (shapesLock)
                    {
                        shapes.Remove(shapeId);
                    }
                }
            }

            lock (timerLock)
            {
                busyRefresh = false;
            }
        }

        /// <summary>
        /// Gets the error message from the provided exception.
        /// </summary>
        /// <param name="ex">Exception</param>
        /// <returns>The error message.</returns>
        private string GetExceptionMessageText(Exception ex)
        {
            string errorMessage = ex.Message;
            if (ex.InnerException != null)
            {
                errorMessage += String.Format("\r\nInner exception:\r\n{0}", GetExceptionMessageText(ex.InnerException));
            }

            return errorMessage;
        }
    }
}
