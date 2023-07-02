
using System;
using Godot;
using GDC = Godot.Collections;


public class NetworkRandomNumberGenerator : Node
{

    //class_name NetworkRandomNumberGenerator

    public RandomNumberGenerator generator


    public void _Ready()
    {
        generator = new RandomNumberGenerator()

        AddToGroup("network_sync");

    }

    public void SetSeed(int new_seed)
    {
        generator.seed = new_seed;

    }

    public int GetSeed()
    {
        return generator.seed;

    }

    public GDC.Dictionary _SaveState()
    {
        return new GDC.Dictionary()
        {
            state = generator.state,
        };

    }

    public void _LoadState(GDC.Dictionary state)
    {
        generator.state = state["state"];

    }

    func GD.Randomize() -> void:
		generator.Randomize();
	
	func GD.Randi() -> int:
		return generator.Randi();
	
	public int RandiRange(int from, int to)
    {
        return generator.RandiRange(from, to);



    }



}