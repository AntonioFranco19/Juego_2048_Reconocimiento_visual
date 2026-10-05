record ModeloVosk(string Nombre, string CarpetaModelo, string Tamano)
        {
            private const string UrlBaseModelos = "https://alphacephei.com/vosk/models";
            public string FicheroZip => CarpetaModelo + ".zip";
            public string Url => $"{UrlBaseModelos}/{FicheroZip}";
        }