using System.Text;
using UnityEngine;

namespace TacticalHUD.GrenadeIndicator;

public sealed class GUIObject
{
    public Color Color = Color.white;
    public bool Enabled = true;
    public bool IsImage;
    public bool IsScreenImage;
    public float Rotation;
    public float Scale = 1f;
    public Sprite Sprite;
    public Sprite Sprite2;
    public StringBuilder StringBuilder = new();
    public GUIStyle Style;
    public Vector3 WorldPos;
}