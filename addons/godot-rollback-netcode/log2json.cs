
using System;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    public class Log2Json : SceneTree
    {
        public Log2Json()
        {
            GDC.Dictionary arguments = new GDC.Dictionary() { };
            foreach (var argument in OS.GetCmdlineArgs())
            {
                if (argument.Find("=") > -1)
                {
                    var key_value = argument.Split("=");
                    arguments[key_value[0].LStrip("--")] = key_value[1];
                }
            }
            if (!Main(arguments))
            {
                Quit(1);
            }
            else
            {
                Quit();
            }
        }

        public bool Main(GDC.Dictionary arguments)
        {
            if (!arguments.Contains("input"))
            {
                GD.Print("Must pass input file as --input=FILENAME");
                return false;

            }
            if (!arguments.Contains("output"))
            {
                GD.Print("Must pass output file as --output=FILENAME");
                return false;

            }
            return ConvertLog2Json(arguments.Get<string>("input"), arguments.Get<string>("output"));
        }

        public bool ConvertLog2Json(string input_filename, string output_filename)
        {
            var infile = new File();
            if (!infile.FileExists(input_filename))
            {
                GD.Print($"No such input file: {input_filename}");
                return false;
            }
            if (infile.OpenCompressed(input_filename, File.ModeFlags.Read, File.CompressionMode.Fastlz) != Error.Ok)
            {
                GD.Print($"Unable to open input file: {input_filename}");
                return false;

            }

            var outfile = new File();
            if (outfile.Open(output_filename, File.ModeFlags.Write) != Error.Ok)
            {
                infile.Close();
                GD.Print($"Unable to open output file: {output_filename}");
                return false;
            }
            while (!infile.EofReached())
            {
                var data = infile.GetVar();
                outfile.StoreLine(JSON.Print(data));
            }
            infile.Close();
            outfile.Close();

            return true;
        }
    }
}