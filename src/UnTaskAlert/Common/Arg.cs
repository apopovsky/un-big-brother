using System;
using System.Collections.Generic;

namespace UnTaskAlert.Common
{
    public static class Arg
    {
        public static string NotNullOrWhitespace(string arg, string argName)
        {
            if (arg == null)
            {
                throw new ArgumentNullException(argName);
            }

            if (string.IsNullOrWhiteSpace(arg))
            {
                throw new ArgumentException($"{argName} can not be empty or whitespace.", argName);
            }

            return arg;
        }

        public static T NotNull<T>(T arg, string argName)
            where T : class
        {
            return arg ?? throw new ArgumentNullException(argName);
        }

        public static ICollection<T> NotNullOrEmpty<T>(ICollection<T> arg, string argName)
        {
            NotNull(arg, argName);

            if (arg.Count == 0)
            {
                throw new ArgumentException($"{argName} collection can not be empty.", argName);
            }

            return arg;
        }

        public static T InRange<T>(T arg, T min, T max, string argName)
            where T : IComparable
        {
            if (arg.CompareTo(min) < 0)
            {
                throw new ArgumentOutOfRangeException(
                    argName,
                    arg,
                    $"{argName} should be between '{min}' and '{max}'.");
            }

            return arg;
        }

        public static T NotDefault<T>(T arg, string argName)
            where T : struct
        {
            if (EqualityComparer<T>.Default.Equals(arg, default(T)))
            {
                throw new ArgumentException($"{argName} has default value.", argName);
            }

            return arg;
        }
    }
}
