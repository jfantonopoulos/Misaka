using Microsoft.EntityFrameworkCore;
using Misaka.Classes;
using Misaka.Interfaces;
using Misaka.Models.MySQL;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Misaka.Extensions
{
    public static class StandardExtensions
    {
        private static Random RNG = new Random();

        //https://stackoverflow.com/a/1262619
        public static void Shuffle<T>(this IList<T> self)
        {
            int c = self.Count;
            while (c > 1)
            {
                c--;
                int k = RNG.Next(c + 1);
                T value = self[k];
                self[k] = self[c];
                self[c] = value;
            }
        }

        public static TValue GetValue<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> self, TKey key)
        {
            bool success = self.TryGetValue(key, out TValue val);
            if (!success)
                return default(TValue);

            return val;
        }

        public static bool TryUpdate<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> self, TKey key, TValue value)
        {
            TValue currentValue = default(TValue);
            if (self.ContainsKey(key))
            {
                currentValue = self.GetValue(key);
                return self.TryUpdate(key, value, currentValue);
            }
            else
            {
                return self.TryAdd(key, value);
            }
        }

        public static bool GenericsAreEqual<T>(T leftVal, T rightVal) where T : class
        {
            return (leftVal == rightVal);
        }

        //bool res = Comparer<TValue>.Default.Compare(valueToRemove, value);
        /*public static bool TryRemove<T>(this ConcurrentBag<T> self, T valueToRemove)
        {
            bool success = false;
            ConcurrentBag<T> newBag = new ConcurrentBag<T>();
            foreach(T value in self)
            {
                if (GenericsAreEqual(Convert.ChangeType(value, typeof(object)), Convert.ChangeType(valueToRemove, typeof(object))))
                {
                    Console.WriteLine("Found item in bag to remove");
                    Console.WriteLine("Found item in bag to remove");
                    Console.WriteLine("Found item in bag to remove");
                    Console.WriteLine("Found item in bag to remove");
                    success = true;
                }
                else
                    newBag.Add(value);
            }
            self = newBag;
            return success;
        }*/

        private static string TryBold(int num, bool shouldBold)
        {
            if (shouldBold)
                return num.ToString().Bold();
            else
                return num.ToString();
        }

        public static string ToNiceTime(this TimeSpan self, bool shouldBold = true)
        {
            string timeString = "";
            int totalHours = (int) Math.Round(self.TotalHours);
            if (totalHours > 0)
                timeString += ((shouldBold ? totalHours.ToString().Number().Bold() : totalHours.ToString().Number())) + " hours, ";
            if (self.Minutes > 0)
                timeString += ((shouldBold ? self.Minutes.ToString().Number().Bold() : self.Minutes.ToString().Number())) + " minutes, ";
            if (self.Seconds > 0)
                timeString += ((shouldBold ? self.Seconds.ToString().Number().Bold() : self.Seconds.ToString().Number())) + " seconds, ";

            return timeString.Substring(0, timeString.Length - 2);
        }

        /*public static string ToNiceTime(this TimeSpan self, bool shouldBold = true)
        {
            string timeString = "";
            double weeks = 0;
            double months = 0;
            double years = 0;
            double days = self.TotalDays;

            if (self.TotalDays >= 7)
            {
                years = days / 365;
                months = years * 12;
                months = months % 12;
                days = days % 365;
                weeks = days * 7;
                weeks = weeks % 4;
                days = days % 7;
            }

            if (years >= 1)
                timeString += $"{TryBold((int)Math.Round(years), shouldBold)} year{(Math.Round(years) >= 1 ? "s" : "")}, ";

            if (months >= 1)
                timeString += $"{TryBold((int)Math.Round(months), shouldBold)} month{(Math.Round(months) > 1 ? "s" : "")}, ";

            if (weeks >= 1)
                timeString += $"{TryBold((int)Math.Round(weeks), shouldBold)} week{(Math.Round(weeks) > 1 ? "s" : "")}, ";

            if (self.Days != 0 && self.Days % 7 != 0)
                timeString += $"{TryBold((int)Math.Round(days), shouldBold)} day{((int)Math.Round(days) > 1 ? "s" : "")}, ";

            if (self.Hours != 0)
                timeString += $"{TryBold(self.Hours, shouldBold)} hour{(self.Hours > 1 ? "s" : "")}, ";
            if (self.Minutes != 0)
                timeString += $"{TryBold(self.Minutes, shouldBold)} minute{(self.Minutes > 1 ? "s" : "")}, ";
            if (self.Seconds != 0)
                timeString += $"{TryBold(self.Seconds, shouldBold)} second{(self.Seconds > 1 ? "s" : "")}, ";

            return timeString.Substring(0, timeString.Length - 2);
        }*/

        public static bool Contains(this string self, params string[] vals)
        {
            return (vals.FirstOrDefault(x => x.Contains(self)) != null);
        }

        public static string UpperFirstChar(this string str)
        {
            return char.ToUpper(str[0]) + str.Substring(1);
        }

        public static string Number(this string str)
        {
            return string.Format("{0:n0}", int.Parse(str));
        }

        //https://stackoverflow.com/a/7768475
        public static string SpliceText(this string text, int lineLength)
        {
            return Regex.Replace(text, "(.{" + lineLength + "})", "$1" + Environment.NewLine);
        }

        public static Enum ToEnum<T>(this string self)
        {
            for (int i = 0; i < Enum.GetValues(typeof(T)).Length; i++)
            {
                Enum parsedEnum = (Enum)Enum.Parse(typeof(T), i.ToString());
                string enumName = Enum.GetName(typeof(T), parsedEnum);
                if (self == enumName)
                {
                    return parsedEnum;
                }
            }

            return null;
        }
        
        public static string GetAgeText(this DateTime startTime)
        {
        const double ApproxDaysPerMonth = 30.4375;
        const double ApproxDaysPerYear = 365.25;

        /*
        The above are the average days per month/year over a normal 4 year period
        We use these approximations as they are more accurate for the next century or so
        After that you may want to switch over to these 400 year approximations

           ApproxDaysPerMonth = 30.436875
           ApproxDaysPerYear  = 365.2425 

          How to get theese numbers:
            The are 365 days in a year, unless it is a leepyear.
            Leepyear is every forth year if Year % 4 = 0
            unless year % 100 == 1
            unless if year % 400 == 0 then it is a leep year.

            This gives us 97 leep years in 400 years. 
            So 400 * 365 + 97 = 146097 days.
            146097 / 400      = 365.2425
            146097 / 400 / 12 = 30,436875

        Due to the nature of the leap year calculation, on this side of the year 2100
        you can assume every 4th year is a leap year and use the other approximatiotions

        */
        //Calculate the span in days
        int iDays = (DateTime.Now - startTime).Days;

        //Calculate years as an integer division
        int iYear = (int)(iDays / ApproxDaysPerYear);

        //Decrease remaing days
        iDays -= (int) (iYear* ApproxDaysPerYear);

         //Calculate months as an integer division
        int iMonths = (int)(iDays / ApproxDaysPerMonth);

        //Decrease remaing days
        iDays -= (int) (iMonths* ApproxDaysPerMonth);

            return string.Format("{0} years, {1} months, {2} days {3} minutes", iYear, iMonths, iDays, startTime.Minute);

        }

    public static void DetachLocal<T>(this DbContext self, T record, string id)
            where T : class, IDiscordObject
        {
            var local = self.Set<T>().Local.FirstOrDefault(entry => entry.Id == id);
            if (local != null)
                self.Entry(local).State = EntityState.Detached;
            self.Entry(record).State = EntityState.Modified;
        }
    }
}
