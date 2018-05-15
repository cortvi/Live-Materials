using UnityEngine;
using System.Collections;

[RequireComponent ( typeof ( MeshRenderer ) )]
[AddComponentMenu ( "marsh/Live Material" )]
[DisallowMultipleComponent]
public class LiveMaterial : MonoBehaviour
{
	#region VARS

	/*
	[System.Serializable]
	public enum ImageType
	{
		Texture = 0,
		Sprite = 1
	}
	*/

	[System.Serializable]
	public struct LiveTexture
	{
		public int id;              /// Texture property id within the shader
		public string name;         /// Texture property name
		public string desc;         /// Texture property description ( shown name )
		public bool enabled;        /// Whether the texture is up for animation
		public float fps;           /// The animation framerate
		public int currentFrame;    /// The current frame of the animation
		//public float nextTick;      /// Time when image will update ( tick = 1 / fps )
		//public ImageType type;      /// The source image type used for animation
		public string path;         /// The path relative to /resources/ where image are located
		//public bool paused;         /// Whether the texture is paused
		public bool fold;           /// Used for Layout foldout
		public bool load;           /// Whether the textures has been loaded

		public Coroutine task;      /// The IEnumerator taking care of the animation
		//public IEnumerator editorTask;
		public Texture2D[] imgs;    /// Every frame of the animation
		public Texture2D fallback;  /// This texture will reamin when animation is cleaned
	}

	[System.Serializable]
	public struct LiveMat
	{
		public int id;                      /// Material ID within the script
		public string name;                 /// Material name
		public bool enabled;                /// Whether the material is up for animation
		public int count;                   /// The length of the live textures array
		public bool playOnAwake;            /// Whether the materials animations play on awake
		public bool loop;                   /// Whether the texture animation is looping
		public bool offscreenUpdate;        /// Whether the object is updated when not visible
		public Shader shader;               /// The matching shader of the material
		public LiveTexture[] txs;  /// All textures within the material
		public bool isPlaying;              /// Whether the material is playing its animations
		public bool backwards;              /// Whether the material is playing backwards
		public bool shaderChanged;          /// Whether shader changed since last check
		public bool fold;                   /// Used for Layout foldout
	}

	public LiveMat[] mts;

	/// General parameters
	public bool affectGI;  //do changes affect GI ? ( in case of static object )
	public bool log = true;   //are log messages enabled for this object ?
	public bool deepLog;   //should all log messages be shown? ( not only warnings )

	//internal references
	public MeshRenderer rend;
	public bool emissionEnabled;

	#endregion

	/* LIVE MATERIAL PIPELINE
	*--------------------------------------------------------------------------------------------
	* .each texture ( when played ) creates a new coroutine that just plays the animation
	* .if animation stops / pauses, coroutine gets destroyed.
	* .to do so, coroutines are created as objects and stored into the LiveTexture
	* .current-frame will remain if paused but still coroutine will be destroyed
	* --------------------------------------------------------------------------------------------
	* .editorTask property stores the coroutines for materials playing in the editor ( STILL WIP )
	*/

	/* DISCLAIMER
	*---------------------------------------------------------------------------------
	* You are free to edit any script, but please notice that several lines
	* are commented out for the sake of Work In Progress Features or testing.
	* If you wish to decomment them realise that all the code will probbably crash.
	*---------------------------------------------------------------------------------
	*/

	///// jaja salu2

	#region PUBLIC FX

	/// <summary>
	/// Play all given materials animations.
	/// </summary>
	/// <param name="backwards">Play the animations backwards?</param>
	/// <param name="index">Material ID as shown in the inspector. If none is given all materials will be animated</param>
	public void Play (params int[] index)
	{
		if ( index.Length == 0 )   //if none is given, all materials are played
		{
			var allMaterials = new int[rend.sharedMaterials.Length];   //array containing all materials' index
			for ( int i = 0; i != allMaterials.Length; i++ )
				allMaterials[i] = i;

			LoopMaterial ( "play", allMaterials );
		}
		else   //played materials given
		{
			LoopMaterial ( "play", index );
		}
	}

	/// <summary>
	/// Pause all given materials animations.
	/// </summary>
	/// <param name="index">Material ID as shown in the inspector. If none is given all materials will be paused.</param>
	public void Pause (params int[] index)
	{
		if ( index.Length == 0 )   //if none is given, all materials are paused
		{
			var allMaterials = new int[rend.sharedMaterials.Length];   //array containing all materials' index
			for ( int i = 0; i != allMaterials.Length; i++ )
				allMaterials[i] = i;

			LoopMaterial ( "pause", allMaterials );
		}
		else   //pause materials given
		{
			LoopMaterial ( "pause", index );
		}
	}

	/// <summary>
	/// Stops all given materials animations ( same as pausing them, but it also resets the animations to their first frame ).
	/// </summary>
	/// <param name="index">Material ID as shown in the inspector. If none is given all materials will be paused.</param>
	public void Stop (params int[] index)
	{
		if ( index.Length == 0 )   //if none is given, all materials are stopped
		{
			var allMaterials = new int[rend.sharedMaterials.Length];   //array containing all materials' index
			for ( int i = 0; i != allMaterials.Length; i++ )
				allMaterials[i] = i;

			LoopMaterial ( "stop", allMaterials );
		}
		else   //stop materials given
		{
			LoopMaterial ( "stop", index );
		}
	}

	/// <summary>
	/// Jumps into given frame.
	/// </summary>
	/// <param name="frame">The frame to jump into.</param>
	/// <param name="matID">The material ID as shown in the inspector.</param>
	/// <param name="txID">All the texture ID as shown in the inspector. If none ID is given all textures within the material will jump into given frame.</param>
	public void GoFrame (int frame, int matID, params int[] txID)
	{
		if ( txID.Length == 0 )
		{
			txID = new int[mts[matID].count];
			for ( int i = 0; i != txID.Length; i++ )
				txID[i] = i;
		}

		for ( int i = 0; i != txID.Length; i++ )
		{
			if ( mts[matID].txs[txID[i]].load == false )
			{
				if ( deepLog )
					Debug.Log ( "Texture " + mts[matID].txs[txID[i]].desc + " skipped [no animation loaded]" );
				continue;
			}

			//Controls that frame is not out of array
			mts[matID].txs[txID[i]].currentFrame = Mathf.Clamp ( frame, 1, mts[matID].txs[txID[i]].imgs.Length );
		}
	}

	/// <summary>
	/// Set the framerate for the given animation.
	/// </summary>
	/// <param name="fps">The framerate to assing.</param>
	/// <param name="matID">The material ID as shown in the inspector.</param>
	/// <param name="txID">All the texture ID as shown in the inspector.If none ID is given all textures within the material will change their framerate.</param>
	public void SetFPS (float fps, int matID, params int[] txID)
	{
		if ( txID.Length == 0 )
		{
			txID = new int[mts[matID].count];
			for ( int i = 0; i != txID.Length; i++ )
				txID[i] = i;
		}

		for ( int i = 0; i != txID.Length; i++ )
		{
			mts[matID].txs[txID[i]].fps = Mathf.Clamp ( fps, 0.01f, Mathf.Infinity );
		}
	}

	/// <summary>
	/// Loads an array of images as animation into a material texture.
	/// </summary>
	/// <param name="animation">The array of textures to load into the animation</param>
	/// <param name="matID">The material ID as shown in the inspector.</param>
	/// <param name="txID">The texture ID as shown in the inspector.</param>
	public void LoadAnimation (Texture2D[] animation, int matID, int txID)
	{
		if ( animation != null )
		{
			mts[matID].txs[txID].imgs = animation;
			SetTextureState ( true, matID, txID );
			mts[matID].txs[txID].load = true;
			mts[matID].txs[txID].path = null;
		}
		else Debug.LogWarning ( "Texture array not valid ! Check arguments.", gameObject );
	}

	/// <summary>
	/// Sets the state ( enabled / disabled ) of a texture. Disabling a texture will keep its animation,
	/// and show up fallback texture. If you enable a texture it will play if its material is already playing.
	/// </summary>
	/// <param name="enabled">Whether to enable or disable the texture.</param>
	/// <param name="matID">The material ID as shown in the inspector.</param>
	/// <param name="txID">The texture ID as shown in the inspector.</param>
	public void SetTextureState (bool enabled, int matID, int txID)
	{
		if ( mts[matID].isPlaying && !enabled )   //if enabled and material playing, stop that texture
		{
			if ( mts[matID].txs[txID].task != null )
				StopCoroutine ( mts[matID].txs[txID].task );

			rend.sharedMaterials[matID].SetTexture ( mts[matID].txs[txID].name, mts[matID].txs[txID].fallback );
		}

		mts[matID].txs[txID].enabled = enabled;

		if ( mts[matID].isPlaying && enabled )   //if enabled and material playing, continue playing
		{
			mts[matID].txs[txID].task = StartCoroutine ( IE_Play ( matID, txID ) );
		}

		if ( mts[matID].txs[txID].name == "_EmissionMap" )
			emissionEnabled = enabled;
	}

	/// <summary>
	/// Deletes the animation for the given texture.
	/// </summary>
	/// <param name="matID">The material ID as shown in the inspector.</param>
	/// <param name="txID">The texture ID as shown in the inspector.</param>
	public void ClearTexture (int matID, int txID)
	{
		if (IsPlaying (matID))   
		{
			if ( log ) Debug.LogWarning ( "A texture animation was cleared during play. This may cause unexpected visual results.", gameObject );
			if ( mts[matID].txs[txID].task != null )
				StopCoroutine ( mts[matID].txs[txID].task );
		}

		mts[matID].txs[txID].imgs = null;
		mts[matID].txs[txID].load = false;
		rend.sharedMaterials[matID].SetTexture ( mts[matID].txs[txID].name, mts[matID].txs[txID].fallback );
	}

	/// <summary>
	/// Sets the global properties of a Live Material.
	/// </summary>
	/// <param name="matID">The material ID as shown in the inspector.</param>
	/// <param name="loop">Whether the animation keeps looping.</param>
	/// <param name="playOnAwake">Whether the material starts playing when GameObject is created.</param>
	/// <param name="offscreenUpdate">Whether the material keeps playing when offscreen.</param>
	/// <param name="backwards">Whether material animations play backwards.</param>
	public void ConfigMaterial (int matID, bool loop, bool playOnAwake, bool offscreenUpdate, bool backwards)
	{
		mts[matID].loop = loop;
		mts[matID].playOnAwake = playOnAwake;
		mts[matID].offscreenUpdate = offscreenUpdate;
		mts[matID].backwards = backwards;
	}

	/// <summary>
	/// Returns TRUE or FALSE based on whether the given material is currently playing.
	/// </summary>
	/// <param name="matID">The material ID as shown in the inspector.</param>
	public bool IsPlaying (int matID)
	{
		return mts[matID].isPlaying;
	}

	#endregion

	#region PRIVATE FX

	//internal function for handling animation flow
	private void LoopMaterial (string action, params int[] index)   /// Loops thruoughout the material in one function to void repeating a lot of code
	{
		if ( mts == null )
		{
			Debug.LogWarning ( "Object has no Live Materials set up !", gameObject );
			return;   //end
		}

		for ( int i = 0; i != index.Length; i++ )
		{
			if ( mts[index[i]].txs == null )
			{
				if ( log )
					Debug.LogWarning ( "material " + mts[index[i]].name + " skippep [no textures set up]", gameObject );
				continue;
			}

			if ( mts[index[i]].enabled == false )
			{
				if ( deepLog )
					Debug.Log ( "material " + mts[index[i]].name + " skipped [not enabled]", gameObject );
				continue;
			}

			if ( mts[index[i]].isPlaying )
			{
				if ( action == "play" )
				{
					if ( deepLog )
						Debug.Log ( "material " + mts[index[i]].name + " won't play [already playing]", gameObject );
					continue;
				}
			}
			else
			{
				if ( action == "pause" || action == "stop" )
				{
					if ( deepLog )
						Debug.Log ( "material " + mts[index[i]].name + " won't stop/pause [not playing]", gameObject );
					continue;
				}
			}

			bool anyTexturePlayed = false;
			bool backwards = mts[index[i]].backwards;

			for ( int tx = 0; tx != mts[index[i]].count; tx++ )
			{
				if ( mts[index[i]].txs[tx].imgs == null || ( mts[index[i]].txs[tx].load == false && mts[index[i]].txs[tx].enabled == true ) )
				{
					if ( log )
						Debug.LogWarning ( "material " + mts[index[i]].name + " skippep " + mts[index[i]].txs[tx].desc + " [texture animation not set up]", gameObject );
					continue;
				}
				if ( mts[index[i]].txs[tx].enabled == false )
				{
					if ( deepLog )
						Debug.Log ( "material " + mts[index[i]].name + " skippep " + mts[index[i]].txs[tx].desc + " [texture not enabled]", gameObject );
					continue;
				}
				if ( mts[index[i]].txs[tx].fps <= 0 )
				{
					if ( log )
						Debug.Log ( "material " + mts[index[i]].name + " skippep " + mts[index[i]].txs[tx].desc + " [framerate must be > 0]", gameObject );
					continue;
				}

				if ( action == "play" )
				{
					//handle editor / runtime playing
					if ( false )
					{
						//materials[index[i]].liveTextures[tx].editorTask = IE_Play ( index[i], tx, backwards );
					}
					else
					{
						mts[index[i]].backwards = backwards;
						mts[index[i]].txs[tx].task = StartCoroutine ( IE_Play ( index[i], tx ) );
						anyTexturePlayed = true;
					}
				}
				else if ( action == "pause" || action == "stop" )
				{

					//handle editor<->runtime animation
					if ( false )
					{
						//StopCoroutine ( materials[index[i]].liveTextures[tx].editorTask );
					}
					else
					{
						StopCoroutine ( mts[index[i]].txs[tx].task );
					}

					if ( action == "stop" )   //if stopped instead of paused, resets animation
					{
						if ( !backwards ) mts[index[i]].txs[tx].currentFrame = 1;
						else mts[index[i]].txs[tx].currentFrame = mts[index[i]].txs[tx].imgs.Length;

						UpdateTexture ( index[i], tx );
					}
				}
			}

			if ( action == "play" && anyTexturePlayed ) mts[index[i]].isPlaying = true;
			else if ( action == "pause" || action == "stop" ) mts[index[i]].isPlaying = false;
		}
	}

	private IEnumerator IE_Play (int mat, int tx)
	{
		while ( true )
		{
			var backwards = mts[mat].backwards;   //update 'backwards' at runtime
			yield return new WaitForSeconds ( 1 / mts[mat].txs[tx].fps );

			//keeping WaitForSeconds before Offscreen check may avoid crashing
			if ( !mts[mat].offscreenUpdate && !rend.isVisible )   //if not updating when offscreen and not visible : skip loop
				continue;

			if ( FinishedLoop ( mts[mat].txs[tx], backwards ) )
			{
				if ( mts[mat].loop )   //finished animation but it's looping
				{
					if ( !backwards ) mts[mat].txs[tx].currentFrame = 0;   //sets to 0 it will sum+1 after anyways
					else mts[mat].txs[tx].currentFrame = mts[mat].txs[tx].imgs.Length + 1;   //same
				}
				else   //finished animation but isn't looping
				{
					mts[mat].isPlaying = false;
					yield break;
				}
			}

			if ( backwards )
				mts[mat].txs[tx].currentFrame--;
			else
				mts[mat].txs[tx].currentFrame++;

			UpdateTexture ( mat, tx );
		}
	}

	private bool FinishedLoop (LiveTexture tx, bool backwards)   /// Did the animation finished its loop?
	{
		if ( !backwards && tx.currentFrame == tx.imgs.Length )
			return true;
		else if ( backwards && tx.currentFrame == 1 )
			return true;

		return false;
	}

	private void UpdateTexture (int mat, int tx)
	{
		rend.sharedMaterials[mat].SetTexture ( mts[mat].txs[tx].name, mts[mat].txs[tx].imgs[mts[mat].txs[tx].currentFrame - 1] );

		if ( affectGI )
		{
			if ( !rend.sharedMaterials[mat].IsKeywordEnabled ( "_EMISSION" ) )   //in case emission is disabled
				rend.sharedMaterials[mat].EnableKeyword ( "_EMISSION" );

			//only update when emission map is changed or albedo in case emission isn't enabled
			if ( mts[mat].txs[tx].name == "_EmissionMap" || ( mts[mat].txs[tx].name == "_MainTex" && !emissionEnabled ) )
				DynamicGI.UpdateMaterials ( rend );
		}
	}

	void OnEnable ()   /// Plays all animation set to play on awake
	{
		rend = GetComponent<MeshRenderer> ();

		if ( mts != null )
		{
			for ( int mat = 0; mat != mts.Length; mat++ )
			{
				if ( mts[mat].txs == null ) continue;
				if ( !mts[mat].enabled ) continue;

				if ( mts[mat].playOnAwake ) Play ( mat );
			}
		}
	}

	private void OnDisable ()
	{
		Stop ();
	}

	#endregion
}