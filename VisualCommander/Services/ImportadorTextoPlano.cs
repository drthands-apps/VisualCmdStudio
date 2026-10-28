using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VisualCommander.Models;

namespace VisualCommander.Services
{
    /// <summary>
    /// Importa archivos .ps1 y .sh como texto plano. Cada línea no vacía
    /// se convierte en una LineaSesion. Sin clasificación específica:
    /// el parser las tratará como Texto (o lo que pueda reconocer por
    /// analogía con CMD).
    /// </summary>
    public static class ImportadorTextoPlano
    {
        public static List<LineaSesion> Importar(string ruta)
        {
            var texto = File.ReadAllText(ruta, Encoding.UTF8);
            var lineas = texto.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            var resultado = new List<LineaSesion>();
            var dir = Environment.CurrentDirectory;

            foreach (var linea in lineas)
            {
                if (string.IsNullOrWhiteSpace(linea)) continue;

                resultado.Add(new LineaSesion
                {
                    Comando = linea,
                    DirectorioTrabajo = dir
                });
            }

            return resultado;
        }
    }
}