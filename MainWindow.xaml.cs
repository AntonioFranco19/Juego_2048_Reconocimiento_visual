using Juego_2048.Models;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System;
using System.ComponentModel;
using System.Linq;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using NAudio.Wave;
using Vosk;
using Microsoft.Web.WebView2.Core;



namespace Juego_2048
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Border[,] _celdas = new Border[4, 4];
        private readonly TextBlock[,] _textos = new TextBlock[4, 4];
        private readonly Tablero _tablero = new Tablero();
        private bool _victoriaAvisada;
        private bool _partidaTerminada;
        private const double Ancho = 640, Alto = 480;


        private static readonly string[] Palabras = { "arriba", "abajo", "izquierda", "derecha", "salir", "reiniciar", "cambiar" };

        private Model? _voskModel;
        private VoskRecognizer? _voskRecognizer;
        private WaveIn? _waveIn;


        private const double ConfianzaMinima = 0.6;

        private static readonly (int Valor, Color Fondo, Color Texto)[] Colores =
        {
            (2,    Color.FromRgb(0xEE, 0xE4, 0xDA), Color.FromRgb(0x77, 0x6E, 0x65)),
            (4,    Color.FromRgb(0xED, 0xE0, 0xC8), Color.FromRgb(0x77, 0x6E, 0x65)),
            (8,    Color.FromRgb(0xF2, 0xB1, 0x79), Colors.White),
            (16,   Color.FromRgb(0xF5, 0x95, 0x63), Colors.White),
            (32,   Color.FromRgb(0xF6, 0x7C, 0x5F), Colors.White),
            (64,   Color.FromRgb(0xF6, 0x5E, 0x3B), Colors.White),
            (128,  Color.FromRgb(0xED, 0xCF, 0x72), Colors.White),
            (256,  Color.FromRgb(0xED, 0xCC, 0x61), Colors.White),
            (512,  Color.FromRgb(0xED, 0xC8, 0x50), Colors.White),
            (1024, Color.FromRgb(0xED, 0xC5, 0x3F), Colors.White),
            (2048, Color.FromRgb(0xED, 0xC2, 0x2E), Colors.White),
        };
        private static readonly Color ColorVacia = Color.FromRgb(0xCD, 0xC1, 0xB4);

        // Los 21 puntos (landmarks) que devuelve MediaPipe, en este orden.
        private static readonly string[] Nombres =
        {
        "Muñeca",
        "Pulgar CMC",   "Pulgar MCP",   "Pulgar IP",   "Pulgar punta",
        "Índice MCP",   "Índice PIP",   "Índice DIP",  "Índice punta",
        "Corazón MCP",  "Corazón PIP",  "Corazón DIP", "Corazón punta",
        "Anular MCP",   "Anular PIP",   "Anular DIP",  "Anular punta",
        "Meñique MCP",  "Meñique PIP",  "Meñique DIP", "Meñique punta",
         };


        public MainWindow()
        {
            InitializeComponent();
            TableroGrid.RowDefinitions.Add(new RowDefinition());
            TableroGrid.RowDefinitions.Add(new RowDefinition());
            TableroGrid.RowDefinitions.Add(new RowDefinition());
            TableroGrid.RowDefinitions.Add(new RowDefinition());
            _tablero.Reiniciar();
            TableroGrid.ColumnDefinitions.Add(new ColumnDefinition());
            TableroGrid.ColumnDefinitions.Add(new ColumnDefinition());
            TableroGrid.ColumnDefinitions.Add(new ColumnDefinition());
            TableroGrid.ColumnDefinitions.Add(new ColumnDefinition());

            CrearGrid();
            ActualizarTablero();
            IniciarVozConVosk();

            Loaded += async (_, _) => await IniciarWebViewAsync(); // Si
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            _voskRecognizer?.Dispose();
            _voskModel?.Dispose();
        }

        private void IniciarVozConVosk()
        {
            try
            {
                string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "model");

                if (!Directory.Exists(modelPath))
                {
                    MostrarEstadoVoz("🎤 No se encontró la carpeta 'model' de Vosk.");
                    return;
                }

                // 1. Cargar el modelo Vosk
                Vosk.Vosk.SetLogLevel(0);
                _voskModel = new Model(modelPath);
                MostrarEstadoVoz("Cargando modelo Vosk...");

                // 2. Gramática restringida: fuerza a Vosk a reconocer solo tus comandos
                string grammarJson = JsonSerializer.Serialize(Palabras.Concat(new[] { "cambiar" }));
                _voskRecognizer = new VoskRecognizer(_voskModel, 16000.0f, grammarJson);

                // 3. Configurar captura de audio con NAudio (16000Hz, 16 bit, Mono)
                _waveIn = new WaveIn
                {
                    WaveFormat = new WaveFormat(16000, 16, 1)
                };

                _waveIn.DataAvailable += WaveIn_DataAvailable;
                _waveIn.StartRecording();

                MostrarEstadoVoz($"🎤 Vosk escuchando — di: {string.Join(", ", Palabras)}");
            }
            catch (Exception ex)
            {
                MostrarEstadoVoz($"🎤 Error al iniciar Vosk: {ex.Message}");
            }
        }

        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_voskRecognizer == null) return;

            // Vosk procesa el búfer de bytes PCM
            if (_voskRecognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
            {
                string jsonResultado = _voskRecognizer.Result();
                ProcesarResultadoVosk(jsonResultado);
            }
        }

        private void ProcesarResultadoVosk(string json)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("text", out var textElement)) return;

            string texto = textElement.GetString()?.Trim().ToLowerInvariant() ?? "";
            if (string.IsNullOrWhiteSpace(texto)) return;

            // Ejecuta las acciones en función del comando detectado
            if (texto == "salir")
            {
                MostrarComandoVoz("⏻ SALIR", Brushes.Firebrick);
                Dispatcher.Invoke(Close);
                return;
            }

            if (texto == "reiniciar")
            {
                MostrarComandoVoz("@ REINICIAR", Brushes.SeaGreen);
                Dispatcher.Invoke(() =>
                {
                    _tablero.Reiniciar();
                    PuntuacionTexto.Text = "0";
                    _partidaTerminada = false;
                    _victoriaAvisada = false;
                    ActualizarTablero();
                });
                return;
            }

            if (texto == "cambiar")
            {
                MostrarComandoVoz(" <-> Cambiar tema", Brushes.SeaGreen);
                Dispatcher.Invoke(() =>
                {
                    if (Background == Brushes.White)
                    {
                        Background = Brushes.Black;
                        txtPuntuacion.Foreground = Brushes.White;
                        PuntuacionTexto.Foreground = Brushes.White;
                        VozComandoTexto.Foreground = Brushes.White;
                        EstadoTexto.Foreground = Brushes.White;
                    }
                    else
                    {
                        Background = Brushes.White;
                        txtPuntuacion.Foreground = Brushes.Black;
                        PuntuacionTexto.Foreground = Brushes.Black;
                        VozComandoTexto.Foreground = Brushes.Black;
                        EstadoTexto.Foreground = Brushes.Black;
                    }

                });
            }

            Direccion? direccion = texto switch
            {
                "arriba" => Direccion.Arriba,
                "abajo" => Direccion.Abajo,
                "izquierda" => Direccion.Izquierda,
                "derecha" => Direccion.Derecha,
                _ => null
            };

            if (direccion != null)
            {
                MostrarComandoVoz($"{FlechaPara(texto)} {texto.ToUpper()}", Brushes.SeaGreen);
                Dispatcher.Invoke(() => ProcesarDireccion(direccion.Value));
            }
        }

        private void CrearGrid()
        {
            for (int f = 0; f < 4; f++)
            {
                for (int c = 0; c < 4; c++)
                {
                    var texto = new TextBlock
                    {
                        FontSize = 24,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Text = _tablero.ObtenerDato(f, c).ToString(),
                    };

                    var border = new Border
                    {
                        Background = new SolidColorBrush(ColorVacia),
                        CornerRadius = new CornerRadius(6),
                        Margin = new Thickness(5),
                        Child = texto,
                    };
                    Grid.SetRow(border, f);
                    Grid.SetColumn(border, c);

                    TableroGrid.Children.Add(border);

                    _celdas[f, c] = border;
                    _textos[f, c] = texto;
                }
            }
        }

        private void Button_Click_R(object sender, RoutedEventArgs e)
        {
            _tablero.Reiniciar();
            PuntuacionTexto.Text = "0";
            _partidaTerminada = false;
            _victoriaAvisada = false;
            ActualizarTablero();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Focus();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            Direccion? direccion = e.Key switch
            {
                Key.Up or Key.W => Direccion.Arriba,
                Key.Down or Key.S => Direccion.Abajo,
                Key.Left or Key.A => Direccion.Izquierda,
                Key.Right or Key.D => Direccion.Derecha,
                _ => null,
            };

            if (direccion != null) ProcesarDireccion(direccion.Value);
        }

        private void ActualizarTablero()
        {
            for (int fila = 0; fila < 4; fila++)
            {
                for (int columna = 0; columna < 4; columna++)
                {
                    int valor = _tablero[fila, columna];
                    Border borde = _celdas[fila, columna];
                    TextBlock texto = _textos[fila, columna];

                    if (valor == 0)
                    {
                        borde.Background = new SolidColorBrush(ColorVacia);
                        texto.Text = string.Empty;
                        continue;
                    }

                    // Valores por encima de 2048 (si se sigue jugando tras ganar)
                    // reutilizan el color de 2048.
                    int valorColor = Math.Min(valor, 2048);
                    var entrada = Array.Find(Colores, e => e.Valor == valorColor);

                    borde.Background = new SolidColorBrush(entrada.Fondo);
                    texto.Foreground = new SolidColorBrush(entrada.Texto);
                    texto.Text = valor.ToString();
                }
            }

            PuntuacionTexto.Text = _tablero.Puntuacion.ToString();
        }
        private void ProcesarDireccion(Direccion direccion)
        {
            if (_partidaTerminada) return;

            bool cambio = _tablero.Mover(direccion);
            if (!cambio) return;

            _tablero.ColocarFichaAleatoria();
            ActualizarTablero();

            // Fase 7: victoria y fin de partida son dos comprobaciones separadas.
            if (!_victoriaAvisada && _tablero.HayFichaGanadora())
            {
                _victoriaAvisada = true;
                MessageBox.Show(this, "¡Has llegado a 2048! Puedes seguir jugando.", "¡Has ganado!", MessageBoxButton.OK);
            }

            if (!_tablero.HayMovimientosPosibles())
            {
                _partidaTerminada = true;
                MessageBox.Show(this, "No quedan movimientos posibles.", "Has perdido", MessageBoxButton.OK);
            }
        }

        private async Task IniciarWebViewAsync() // Si
        {
            try
            {
                string datos = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ManoBasico", "WebView2");
                var entorno = await CoreWebView2Environment.CreateAsync(null, datos);
                await Visor.EnsureCoreWebView2Async(entorno);

                var web = Visor.CoreWebView2;

                // https://app.local/ apunta a la carpeta Web junto al .exe (contexto seguro → cámara permitida)
                web.SetVirtualHostNameToFolderMapping("app.local",
                    Path.Combine(AppContext.BaseDirectory, "Web"),
                    CoreWebView2HostResourceAccessKind.Allow);

                // Concede el permiso de cámara sin preguntar
                web.PermissionRequested += (_, e) =>
                {
                    if (e.PermissionKind == CoreWebView2PermissionKind.Camera)
                        e.State = CoreWebView2PermissionState.Allow;
                };

                web.WebMessageReceived += OnWebMessage;
                web.Navigate("https://app.local/index.html");
            }
            catch (Exception ex)
            {
                EstadoTexto.Text = "Error al iniciar WebView2: " + ex.Message;
            }
        }

        /// <summary>Mensajes que envía la página (JavaScript) a C#.</summary>
        private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e) // Si
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;

            switch (root.GetProperty("type").GetString())
            {
                case "status":
                    EstadoTexto.Text = root.GetProperty("text").GetString();
                    break;

                case "error":
                    EstadoTexto.Text = "Error: " + root.GetProperty("text").GetString();
                    break;

                case "nohand":
                    EstadoTexto.Text = "No se detecta la mano";
                    break;

                case "landmarks":
                    int w = root.GetProperty("w").GetInt32();
                    int h = root.GetProperty("h").GetInt32();
                    var pts = root.GetProperty("pts");   // 63 números: x0, y0, z0, x1, y1, z1, ...

                    var xs = new float[21];
                    var ys = new float[21];

                    // texto.AppendLine(string.Format("{0,2}  {1,-13} {2,6} {3,6} {4,7}   {5,4} {6,4}",
                    //     "N", "Punto", "X", "Y", "Z", "Px", "Py"));

                    for (int i = 0; i < 21; i++)
                    {
                        float x = (float)pts[3 * i].GetDouble();
                        float y = (float)pts[3 * i + 1].GetDouble();
                        //float z = (float)pts[3 * i + 2].GetDouble();
                        xs[i] = x;
                        ys[i] = y;

                        //     texto.AppendLine(FormattableString.Invariant(
                        //         $"{i,2}  {Nombres[i],-13} {x,6:0.000} {y,6:0.000} {z,7:+0.000;-0.000;0.000}   {x * w,4:0} {y * h,4:0}"));
                    }

                    Direccion orden = ReconocerMano(xs, ys);
                    EstadoTexto.Text = orden.ToString();
                    ProcesarDireccion(orden);
                    break;
            }
        }

        private Direccion ReconocerMano(float[] xs, float[] ys) // Si
        {
            Direccion orden = Direccion.Nada;

            for (int i = 4; i < xs.Length; i = i + 4)
            {
                float margenDistancia = 0.08f;
                float indicePuntaX = xs[i];
                float indicePuntaY = ys[i];

                float indiceBaseX = xs[i - 3];
                float indiceBaseY = ys[i - 3];

                float dx = indiceBaseX - indicePuntaX;
                float dy = indiceBaseY - indicePuntaY;

                float distancia = MathF.Sqrt(dx * dx + dy * dy);
                if (distancia < margenDistancia)
                {
                    return Direccion.Nada;
                }

                float anguloRad = MathF.Atan2(-dy, dx);
                float anguloDeg = anguloRad * (180.0f / MathF.PI);

                anguloDeg = (anguloDeg % 360.0f + 360.0f) % 360.0f;

                // 3. Clasificación cardinal
                return anguloDeg switch
                {
                    >= 45.0f and < 135.0f => orden = Direccion.Abajo,
                    >= 135.0f and < 225.0f => orden = Direccion.Izquierda,
                    >= 225.0f and < 315.0f => orden = Direccion.Arriba,
                    _ => orden = Direccion.Derecha
                };
            }
            // Muñeca 0, pulgar base 1, pulgar medio1 2, pulgar medio2 3, pulgar punta 4 ..
            return orden;
        }

        // Flecha equivalente a cada palabra, para no depender solo del
        // texto en pantalla.
        private static string FlechaPara(string texto) => texto switch
        {
            "arriba" => "↑",
            "abajo" => "↓",
            "izquierda" => "←",
            "derecha" => "→",
            "reiniciar" => "@",
            _ => "",
        };

        // Estas dos actualizan controles de la ventana, así que se
        // envuelven en Dispatcher.Invoke: se llaman tanto desde el
        // constructor (hilo de la interfaz, donde Dispatcher.Invoke no
        // hace nada raro) como desde los eventos de voz (hilo del
        // reconocedor, donde SÍ hace falta).
        private void MostrarEstadoVoz(string texto)
        {
            Dispatcher.Invoke(() => EstadoTexto.Text = texto);
        }

        private void MostrarComandoVoz(string texto, Brush color)
        {
            Dispatcher.Invoke(() =>
            {
                VozComandoTexto.Text = texto;
                VozComandoTexto.Foreground = color;
            });
        }

    }
}