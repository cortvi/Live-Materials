using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

[CustomEditor ( typeof ( LiveMaterial ) )]

public class LM_GUI : Editor
{

	#region CUSTOM EDITOR

	private void SwitchFold (char type, string label, ref bool checkbox, ref bool fold)
	{
		//box
		Rect box = GUILayoutUtility.GetLastRect ();
		box.height += 20;
		if ( type == 'm' ) GUI.Box ( box, "", GUIStyle.none );
		else GUI.Box ( box, "" );

		//checkbox ( enabled or not )
		Rect chkbox = new Rect ( box.width - 10, box.y + 5, 10, 10 );
		checkbox = GUI.Toggle ( chkbox, checkbox, "" );

		//fold
		if ( checkbox )
		{
			Rect fdout = new Rect ( 30, box.y + 5, 10, 10 );
			fold = EditorGUI.Foldout ( fdout, fold, "" );
		}
		else
		{
			Rect x = new Rect ( 20, box.y + 5, 20, 20 );
			GUI.Label ( x, "X", EditorStyles.boldLabel );
		}
	

		//label
		Rect lbl = new Rect ( 35, box.y + 5, box.width - 50, box.height - 5 );
		if ( type == 'm' ) GUI.Label ( lbl, label, EditorStyles.boldLabel );
		else GUI.Label ( lbl, label );

		//reserve space
		GUILayoutUtility.GetRect ( box.width, box.height );
	}

	#endregion

	private LiveMaterial lm;

	public override void OnInspectorGUI ()
	{
		lm = target as LiveMaterial;
		lm.rend = lm.GetComponent<MeshRenderer> ();

		Undo.RecordObject ( lm, "Live material changed" );

		/// General Parameters ( only shown if static )
		EditorGUILayout.LabelField ( "General Parameters", EditorStyles.boldLabel );
		EditorGUILayout.Space ();

		if ( lm.gameObject.isStatic ) lm.affectGI = EditorGUILayout.Toggle ( "Affect GI", lm.affectGI );
		else lm.affectGI = false;

		lm.log = EditorGUILayout.Toggle ( "Material debugging", lm.log );

		if ( lm.log ) lm.deepLog = EditorGUILayout.Toggle ( "Extense debugging", lm.deepLog );
		else lm.deepLog = false;
		EditorGUILayout.LabelField ( "- Debugging works per material -", EditorStyles.wordWrappedMiniLabel );

		EditorGUILayout.Separator ();

		/// Material toggle
		
		if ( MaterialsChanged () )
			GetAllMaterials ();

		for ( int i = 0; i != lm.mts.Length; i++ )
		{
			EditorGUILayout.Separator (); //last
			SwitchFold ( 'm', "[" + i + "] " + lm.mts[i].name, ref lm.mts[i].enabled, ref lm.mts[i].fold );
			lm.mts[i].shaderChanged = ShaderChanged ( lm.mts[i] );
			lm.mts[i].shader = lm.rend.sharedMaterials[i].shader;

			if ( lm.mts[i].fold && lm.mts[i].enabled )
			{
				/// Material paramaters
				lm.mts[i].loop = EditorGUILayout.ToggleLeft ( "   Loop", lm.mts[i].loop );
				lm.mts[i].playOnAwake = EditorGUILayout.ToggleLeft ( "   Play on awake", lm.mts[i].playOnAwake );
				lm.mts[i].backwards = EditorGUILayout.ToggleLeft ( "   Play backwards", lm.mts[i].backwards );
				lm.mts[i].offscreenUpdate = EditorGUILayout.ToggleLeft ( "   Update when offscren", lm.mts[i].offscreenUpdate );

				/* EDITOR PLAY WIP
				if ( s2m.materials[i].isPlaying ) /// Pause animations button
				{
					if (GUILayout.Button ("Pause"))
					{
						s2m.Pause ( i );
					}
				}
				else   /// Play animations button
				{
					if (GUILayout.Button ("Play"))
					{
						s2m.Play ( false, i );
					}
				}
				*/

				/// Textures toggle
				EditorGUILayout.Space ();

				if ( lm.mts[i].shaderChanged )
					GetAllTextures ( lm.mts[i] );

				for ( int a = 0; a != lm.mts[i].count; a++ )
				{
					EditorGUILayout.Separator ();   //last
					SwitchFold ( 't', "[" + a + "]   " + lm.mts[i].txs[a].desc, ref lm.mts[i].txs[a].enabled, ref lm.mts[i].txs[a].fold );

					if ( lm.mts[i].txs[a].name == "_EmissionMap" )   //check whether emission map is enabled
						lm.emissionEnabled = lm.mts[i].txs[a].enabled;

					/// Enabled textures foldout
					EditorGUILayout.Separator ();
					if ( lm.mts[i].txs[a].fold && lm.mts[i].txs[a].enabled )
					{
						if ( lm.mts[i].txs[a].load )   /// If images are already loaded
						{
							/// Playback Engine GUI
							lm.mts[i].txs[a].fps = EditorGUILayout.FloatField ( "Framerate", lm.mts[i].txs[a].fps );
							lm.mts[i].txs[a].currentFrame = EditorGUILayout.IntSlider ( lm.mts[i].txs[a].currentFrame, 1, lm.mts[i].txs[a].imgs.Length );
							lm.rend.sharedMaterials[i].SetTexture ( lm.mts[i].txs[a].name, lm.mts[i].txs[a].imgs[lm.mts[i].txs[a].currentFrame - 1] as Texture2D );

							bool nullTex = false;

							if ( lm.mts[i].txs[a].path != null )
							{
								EditorGUILayout.SelectableLabel ( "Assets/" + lm.mts[i].txs[a].path );
								foreach ( var t in lm.mts[i].txs[a].imgs )
									if ( t == null ) nullTex = true;
							}
							else EditorGUILayout.LabelField ( "Animation loaded by user." );
							EditorGUILayout.LabelField ( lm.mts[i].txs[a].imgs.Length.ToString () + " images loaded." );
							if (GUILayout.Button ("Clear"))   /// Clear image array button
							{
								lm.ClearTexture ( i, a );
							}
							if ( nullTex )
								EditorGUILayout.LabelField ( "- this animation contains null images, consider reloading it -", EditorStyles.wordWrappedMiniLabel );
						}
						else   /// If images aren't yet laoded
						{
							lm.mts[i].txs[a].path = EditorGUILayout.TextField ( "Path", lm.mts[i].txs[a].path );
							EditorGUILayout.LabelField ( "Assets/" + lm.mts[i].txs[a].path );
							lm.rend.sharedMaterials[i].SetTexture ( lm.mts[i].txs[a].name, lm.mts[i].txs[a].fallback );
							if ( GUILayout.Button ( "Load" ) || GotReturnUp () )
							{
								LoadAnimation ( lm.mts[i].txs[a].path, i, a );
							}
						}
						EditorGUILayout.Separator ();

						EditorGUILayout.LabelField ( "Fallback texture" );
						lm.mts[i].txs[a].fallback = (Texture2D) EditorGUILayout.ObjectField ( lm.mts[i].txs[a].fallback, typeof ( Texture2D ), true );
						EditorGUILayout.Separator ();
					}
				}
			}
		}
	}

	private void LoadAnimation ( string path, int matID, int txID )
	{
		if ( AssetDatabase.IsValidFolder ( "Assets/" + path ) )
		{
			var guid = AssetDatabase.FindAssets ( "t:texture2D", new string[] { "Assets/" + path } );
			if ( guid.Length == 0 || guid == null )
			{
				Debug.LogWarning ( "Couldn't load animation ! Maybe folder path is wrong.", lm.gameObject );
				return;
			}
			else
			{
				lm.mts[matID].txs[txID].imgs = new Texture2D[guid.Length];
				for ( int i = 0; i != guid.Length; i++ )
					lm.mts[matID].txs[txID].imgs[i] = ( Texture2D ) AssetDatabase.LoadAssetAtPath ( AssetDatabase.GUIDToAssetPath ( guid[i] ), typeof ( Texture2D ) );

				lm.SetTextureState ( true, matID, txID );
				lm.mts[matID].txs[txID].load = true;
				lm.mts[matID].txs[txID].path = path;
			}
		}
		else
		{
			Debug.LogWarning ( "Couldn't find folder !", lm.gameObject );
			return;
		}
	}

	private void GetAllTextures (LiveMaterial.LiveMat mat)
	{
		var n = ShaderUtil.GetPropertyCount ( mat.shader );
		var txs = new List<LiveMaterial.LiveTexture> ();

		/// Keeps texture properties within the shader into a list
		var count = 0;
		for ( int i = 0; i != n; i++ )
		{
			if ( ShaderUtil.GetPropertyType ( mat.shader, i ) == ShaderUtil.ShaderPropertyType.TexEnv )
			{
				var tx = new LiveMaterial.LiveTexture ();
				tx.id = i;
				tx.name = ShaderUtil.GetPropertyName ( mat.shader, i );
				tx.desc = ShaderUtil.GetPropertyDescription ( mat.shader, i );
				tx.currentFrame = 1;
				tx.fps = 30;
				tx.fold = true;
				tx.load = false;

				txs.Add ( tx );

				count++;
			}
		}

		lm.mts[mat.id].txs = txs.ToArray ();
		txs.Clear ();
		lm.mts[mat.id].count = count;
	}

	private bool ShaderChanged (LiveMaterial.LiveMat mat)
	{
		if ( mat.txs == null || mat.shader != lm.rend.sharedMaterials[mat.id].shader )
		{
			return true;
		}
		else
		{
			return false;
		}
	}

	private bool MaterialsChanged ()
	{
		if ( lm.mts == null || lm.mts.Length != lm.rend.sharedMaterials.Length )
			return true;

		foreach ( var m in lm.mts )
			if ( m.name != lm.rend.sharedMaterials[m.id].name )
				return true;

		return false;
	}

	private void GetAllMaterials ()   /// If material changed or not registered, create new array
	{
		var count = lm.rend.sharedMaterials.Length;
		lm.mts = new LiveMaterial.LiveMat[count];

		for ( int i = 0; i != lm.mts.Length; i++ )
		{
			lm.mts[i].id = i;
			lm.mts[i].name = lm.rend.sharedMaterials[i].name;
			lm.mts[i].fold =  true;
		}
	}

	private bool GotReturnUp ()
	{
		if ( Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.Return )
			return true;
		else
			return false;
	}

	/* EDITOR PLAY MATERIALS WIP
	void update ()
	{
		if ( !EditorApplication.isPlaying && s2m != null )
		{
			if ( s2m.materials != null )
			{
				for ( int i = 0; i != s2m.materials.Length; i++ )   //foreach given material
				{
					if ( s2m.materials[i].liveTextures == null ) continue;
					if ( !s2m.materials[i].enabled ) continue;
					if ( s2m.materials[i].isPlaying ) continue;

					for ( int tx = 0; tx != s2m.materials[i].liveTextures.Length; tx++ )   //foreach texture in given material
					{
						if ( s2m.materials[i].liveTextures[tx].imgs == null ) continue;
						if ( !s2m.materials[i].liveTextures[tx].enabled ) continue;

						if ( s2m.materials[i].liveTextures[tx].editorTask == null ) continue;

						s2m.materials[i].liveTextures[tx].editorTask.MoveNext ();
					}
				}
			}
		}
	}

	void OnEnable () { EditorApplication.update += update; }
	void OnDisable () { EditorApplication.update -= update; }

	*/
}