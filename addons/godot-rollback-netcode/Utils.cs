
using System;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    public static class Utils
    {
        public static string Snake2Pascal(string str)
        {
            string pascalString = "";
            bool capitalizeNext = true;
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == '_')
                {
                    if (i == 0)
                    {
                        pascalString += "_";
                    }
                    else
                    {
                        capitalizeNext = true;
                    }
                }
                else if (capitalizeNext)
                {
                    pascalString += str[i].ToString().Capitalize();
                    capitalizeNext = false;
                }
                else
                {
                    pascalString += str[i];
                }
            }
            return pascalString;

        }

        public static bool HasInteropMethod(Godot.Object obj, string snakeCaseMethod)
        {
            return obj.HasMethod(snakeCaseMethod) || obj.HasMethod(Snake2Pascal(snakeCaseMethod));
        }

        public static object CallInteropMethod(Godot.Object obj, string snakeCaseMethod, GDC.Array argArray = null)
        {
            if (argArray == null)
                argArray = new GDC.Array() { };
            if (obj.HasMethod(snakeCaseMethod))
            {
                return obj.Callv(snakeCaseMethod, argArray);
            }
            return obj.Callv(Snake2Pascal(snakeCaseMethod), argArray);
        }

        public static int GDKeyComparer(object a, object b)
        {
            if (a is string aString && b is string bString)
                return aString.CompareTo(bString);
            else if (a is int aInt && b is int bInt)
                return aInt.CompareTo(bInt);
            else
                return a is string ? 1 : -1;
        }
    }
}