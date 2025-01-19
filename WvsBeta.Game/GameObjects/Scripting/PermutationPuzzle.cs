using System;
using System.Linq;

namespace WvsBeta.Game
{
	public enum PermutationResult
	{
		Correct,
		Wrong,
		NotEnoughPlayers
	}

	public class PermutationPuzzle
	{
		private Map Map { get; }
		private string Key { get; }
		private int Players { get; }

		private FieldSet FieldSet => Map.ParentFieldSet;

		public bool Created = false;
		private int[] _permutation;

		public PermutationPuzzle(Map map, string key, int players)
		{
			Map = map;
			Key = key;
			Players = players;

			if (FieldSet.GetVar(Key) == null)
			{
				Created = true;
				GenerateRandomPermutation();
				SaveToFieldSet();
			}
			else
			{
				LoadFromFieldSet();
			}
		}

		private void SaveToFieldSet()
		{
			FieldSet.SetVar(Key, string.Join("", _permutation));
		}

		private void LoadFromFieldSet()
		{
			_permutation = FieldSet.GetVar(Key).Select(c => int.Parse(c.ToString())).ToArray();
		}

		private void GenerateRandomPermutation()
		{
			_permutation = Enumerable.Range(0, Map.MapAreas.Count).Select(i => 0).ToArray();

			for (var i = 0; i < Players; i++)
			{
				_permutation[i] = 1;
			}

			_permutation.Shuffle();
		}

		public PermutationResult AreaCheck()
		{
			var areas = Map.CharactersInAreas().Values.ToArray();

			if (areas.Count(i => i > 0) < Players)
			{
				return PermutationResult.NotEnoughPlayers;
			}

			return areas.SequenceEqual(_permutation) ? PermutationResult.Correct : PermutationResult.Wrong;
		}

	}
}