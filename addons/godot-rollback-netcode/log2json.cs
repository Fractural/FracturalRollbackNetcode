
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;


public class log2json : SceneTree
{
	 
	public void _Init()
	{  
		Dictionary arguments = new Dictionary(){};
		foreach(var argument in OS.GetCmdlineArgs())
		{
			if(argument.Find("=") > -1)
			{
				var key_value = argument.Split("=");
				arguments[key_value[0].Lstrip("--")] = key_value[1];
		
			}
		}
		if(!main(arguments))
		{
			Quit(1);
		}
		else
		{
			Quit();
	
		}
	}
	
	public bool Main(Dictionary arguments)
	{  
		if(!arguments.Has("input"))
		{
			Print ("Must pass input file as --input=FILENAME");
			return false;
		
		}
		if(!arguments.Has("output"))
		{
			Print ("Must pass output file as --output=FILENAME");
			return false;
	
		}
		return Log2json(arguments["input"], arguments["output"]);
	
	}
	
	public bool Log2json(String input_filename, String output_filename)
	{  
		var infile  = new File()
		if(!infile.FileExists(input_filename))
		{
			Print ("No such input file: %s" % input_filename);
			return false;
		}
		if(infile.OpenCompressed(input_filename, File.READ, File.COMPRESSION_FASTLZ) != OK)
		{
			Print ("Unable to open input file: %s" % input_filename);
			return false;
		
		}
		var outfile  = new File()
		if(outfile.Open(output_filename, File.WRITE) != OK)
		{
			infile.Close();
			Print ("Unable to open output file: %s" % output_filename);
			return false;
		
		}
		while(!infile.EofReached())
		{
			var data = infile.get_var()
			outfile.StoreLine(JSON.Print(data));
		
		}
		infile.Close();
		outfile.Close();
		
		return true;
	
	
	}
	
	
	
}