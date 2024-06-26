using System;
using System.Reflection;
using System.ComponentModel;
using Godot;

namespace FortniteMonopolyExtensions
{
    public static class Extensions
    {
        readonly static Random _rng = new();

        public static string GetDescription<TEnum>(this TEnum enumVal) where TEnum : Enum
        {
            try
            {
                MemberInfo[] memInfo = enumVal.GetType().GetMember(enumVal.ToString());
                Attribute description = memInfo[0].GetCustomAttribute(typeof(DescriptionAttribute), false);
                return (description as DescriptionAttribute).Description;
            }
            catch {
                GD.PrintErr($"Failed to get description of {enumVal}");
                return null;
            }
        }

        // Fisher-Yates Shuffle
        public static void Shuffle<T>(this T[] array)
        {
            int n = array.Length;
            while (n > 1)
            {
                n--;
                int k = _rng.Next(n + 1);
                (array[n], array[k]) = (array[k], array[n]);
            }
        }
    }
}