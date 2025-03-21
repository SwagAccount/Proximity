using System;
using System.ComponentModel;

namespace Sandbox;

[Title( "Post Process From Shader" )]
[Category( "Post Processing" )]
[Icon( "grain" )]
public sealed class PostProcessFromShader : PostProcess, Component.ExecuteInEditor
{
	[Property] Shader Shader { get; set; }
	[Property] Vector2 Resolution { get; set; } = new( 320, 240 );
	[Property, Range( 0, 8 )] float ColorDepth { get; set; }
	
	[Property, ToggleGroup( "Tint", Label = "Tint" )] bool Tint { get; set; }
	[Property, ToggleGroup( "Tint" )] Color TintColor { get; set; } = new(1, 1, 1);
	
	[Property, ToggleGroup( "Dithering", Label = "Dithering" )] bool Dithering { get; set; }
	[Property, Range( 1, 8 ), ToggleGroup( "Dithering" )] float DitherScale { get; set; }
	[Property, ToggleGroup( "Dithering" )] Texture BayerMatrix { get; set; }

	IDisposable renderHook;

	protected override void OnEnabled()
	{
		renderHook = Camera.AddHookBeforeOverlay( "My Post Processing", 1000, RenderEffect );
	}

	protected override void OnDisabled()
	{
		renderHook?.Dispose();
		renderHook = null;
	}

	RenderAttributes attributes = new();
	
	public void RenderEffect( SceneCamera camera )
	{
		if ( !camera.EnablePostProcessing )
			return;

		//attributes.Set( "g_vInternalResolution", Resolution );
		
		Graphics.GrabFrameTexture( "ColorBuffer", attributes );
		
		Material mat = Material.FromShader( Shader );
		
		mat.Set( "g_ColorDepth", ColorDepth );
		mat.Set( "g_vInternalResolution", Resolution );
		
		mat.Set( "g_ditherenabled", Dithering );
		mat.Set( "g_DitherScale", DitherScale );
		mat.Set( "g_tBayerMatrix", BayerMatrix );

		mat.Set( "F_TINT", Tint );
		mat.Set( "g_vTintColor", new Vector3( TintColor.r, TintColor.g, TintColor.b) );
		
		Graphics.Blit( mat, attributes );
	}
}
