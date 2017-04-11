using UnityEngine;
using UnityEditor;
using System.Collections;

public class LM_INFO : EditorWindow
{
	public const double version = 1.2;
	public Texture2D icon;

	[MenuItem ( "Window/About Live Materials" )]
	static void Window ()
	{
		var win = GetWindow (typeof (LM_INFO), true, "About Live Materials");
		win.minSize = new Vector2 ( 500, 250 );
		win.maxSize = win.minSize;
		win.Show ();
	}

	void OnGUI ()
	{
		GUI.Label ( new Rect ( 280, 0, icon.width, icon.height ), icon );
		GUI.Label ( new Rect ( 10, 20, 270, 150 ), "Live materials is a Unity Asset \nprovided by marsh ( me ). \n\n" +
																"If you like it consider giving \nsome feedback on the asset store.\n\n" +
																"Thanks for purchasing and \nstay tuned for any incoming \nupdate! ^^\n\n" +
																"Twitter — @marsh12th",
																EditorStyles.boldLabel );
		GUI.Label ( new Rect ( 10, 210, 50, 20 ), version.ToString ( "[v0.0]" ) );
	}
}
