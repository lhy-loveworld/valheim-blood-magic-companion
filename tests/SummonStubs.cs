// Test doubles only. Never compiled into the plugin. These do not simulate Unity physics or Harmony loading.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float a, float b, float c) { x=a; y=b; z=c; }
        public static float Distance(Vector3 a, Vector3 b)
        { return (float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z)); }
    }
    public class Component
    {
        public GameObject gameObject;
        public Transform transform { get { return gameObject.transform; } }
        public T GetComponent<T>() where T:class { return gameObject.GetComponent<T>(); }
    }
    public class GameObject
    {
        public string name;
        public Transform transform;
        private readonly List<object> components = new List<object>();
        public GameObject(string n) { name=n; transform=new Transform(); Add(transform); }
        public void Add(Component c) { c.gameObject=this; components.Add(c); }
        public T GetComponent<T>() where T:class
        { foreach(object c in components) if(c is T) return c as T; return null; }
    }
    public class Transform:Component { public Vector3 position; }
    public class Collider:Component
    {
        public Character ParentCharacter;
        public T GetComponentInParent<T>() where T:class { return ParentCharacter as T; }
    }
    public struct RaycastHit { public Collider collider; }
    public static class Physics
    {
        public static bool NativeBlocked;
        public static int NativeCalls, AllCalls, LastMask;
        public static float LastDistance;
        public static Vector3 LastOrigin, LastDirection;
        public static RaycastHit[] Hits = new RaycastHit[0];
        private static void Record(Vector3 o, Vector3 d, float length, int mask)
        { LastOrigin=o; LastDirection=d; LastDistance=length; LastMask=mask; }
        public static bool Raycast(Vector3 o, Vector3 d, float length, int mask)
        { NativeCalls++; Record(o,d,length,mask); return NativeBlocked; }
        public static RaycastHit[] RaycastAll(Vector3 o, Vector3 d, float length, int mask)
        { AllCalls++; Record(o,d,length,mask); return Hits; }
    }
}
public class Character:UnityEngine.Component
{
    public enum Faction { Players=0, Undead=3, PlayerSpawned=11, TrainingDummy=12 }
    public bool Owner=true, Tamed=true, Dead;
    public float m_lavaHeatLevel, m_ashlandsOceanHeatLevel;
    public Faction Team=Faction.Players;
    public static readonly List<Character> All = new List<Character>();
    public bool IsOwner() { return Owner; }
    public bool IsTamed() { return Tamed; }
    public bool IsDead() { return Dead; }
    public Faction GetFaction() { return Team; }
    public static List<Character> GetAllCharacters() { return All; }
}
public class BaseAI:UnityEngine.Component
{
    public Character Target;
    public Func<Character,bool> Visible = delegate { return true; };
    public Character GetTargetCreature() { return Target; }
    public bool CanSeeTarget(Character target) { return Visible(target); }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch:Attribute { public HarmonyPatch(Type t,string name,Type[] args) {} }
    public class Harmony
    {
        public Harmony CreateClassProcessor(Type t) { return this; }
        public void Patch() { throw new NotSupportedException("Standalone tests do not load Harmony."); }
    }
    public static class AccessTools
    {
        public static FieldInfo Field(Type t,string name)
        { return t.GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static); }
        public static MethodInfo Method(Type t,string name,Type[] args=null)
        {
            const BindingFlags f=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
            return args == null ? t.GetMethod(name,f) : t.GetMethod(name,f,null,args,null);
        }
    }
    public class CodeInstruction
    {
        public OpCode opcode;
        public object operand;
        public readonly List<Label> labels = new List<Label>();
        public readonly List<object> blocks = new List<object>();
        public CodeInstruction(OpCode op,object value=null) { opcode=op; operand=value; }
    }
}
