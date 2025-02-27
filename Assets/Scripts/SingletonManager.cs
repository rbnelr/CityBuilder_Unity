using UnityEngine;
using UnityEditor;

// Single singleton to hold "Systems" or "Managers", which only exist once and need to be referenced from many places
// Do this instead of making each system a singleton because
// -Implementing singletons correctly in unity is somewhat involved and can't be abstracted away trivially
// -accessing each system now can be more concise

[ExecuteAlways]
public class SingletonsManager : MonoBehaviour {

	// config via inspector
	public GameTime game_time;
	public Entities entities;
	public Pathfinding pathfinding;
	
	void OnEnable () {
		Debug.Assert(g.game_time == null); g.game_time = game_time;
		Debug.Assert(g.entities == null); g.entities = entities;
		Debug.Assert(g.pathfinding == null); g.pathfinding = pathfinding;
	}
	void OnDisable () {
		g.game_time = null;
		g.entities = null;
		g.pathfinding = null;
	}
}

// possibly a bit unothodox, but I call this static class 'g' so that you can refer to each singleton system as g.system, which is short and easy to type
public static class g {
	public static GameTime game_time;
	public static Entities entities;
	public static Pathfinding pathfinding;
}
