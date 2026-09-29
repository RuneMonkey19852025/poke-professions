using System.Collections.Generic;

namespace PokeProfessions.Core
{
    public static class TypeChart
    {
        // attacker -> (defender -> multiplier)
        private static readonly Dictionary<Element, Dictionary<Element, float>> chart = new Dictionary<Element, Dictionary<Element, float>>
        {
            [Element.Ember] = new Dictionary<Element, float> { [Element.Verdant] = 2f, [Element.Frost] = 2f, [Element.Toxic] = 2f, [Element.Tide] = 0.5f, [Element.Stone] = 0.5f, [Element.Ember] = 0.5f, [Element.Beast] = 2f },
            [Element.Tide] = new Dictionary<Element, float> { [Element.Ember] = 2f, [Element.Stone] = 2f, [Element.Verdant] = 0.5f, [Element.Tide] = 0.5f, [Element.Volt] = 0.5f },
            [Element.Verdant] = new Dictionary<Element, float> { [Element.Tide] = 2f, [Element.Stone] = 2f, [Element.Ember] = 0.5f, [Element.Toxic] = 0.5f, [Element.Gale] = 0.5f, [Element.Frost] = 0.5f },
            [Element.Volt] = new Dictionary<Element, float> { [Element.Tide] = 2f, [Element.Gale] = 2f, [Element.Volt] = 0.5f, [Element.Stone] = 0.5f },
            [Element.Stone] = new Dictionary<Element, float> { [Element.Ember] = 2f, [Element.Gale] = 2f, [Element.Frost] = 2f, [Element.Verdant] = 0.5f, [Element.Beast] = 0.5f },
            [Element.Gale] = new Dictionary<Element, float> { [Element.Verdant] = 2f, [Element.Toxic] = 2f, [Element.Beast] = 2f, [Element.Volt] = 0.5f, [Element.Stone] = 0.5f },
            [Element.Frost] = new Dictionary<Element, float> { [Element.Verdant] = 2f, [Element.Gale] = 2f, [Element.Beast] = 2f, [Element.Ember] = 0.5f, [Element.Tide] = 0.5f, [Element.Frost] = 0.5f },
            [Element.Toxic] = new Dictionary<Element, float> { [Element.Verdant] = 2f, [Element.Mind] = 2f, [Element.Toxic] = 0.5f, [Element.Stone] = 0.5f, [Element.Shade] = 0.5f },
            [Element.Mind] = new Dictionary<Element, float> { [Element.Toxic] = 2f, [Element.Beast] = 2f, [Element.Shade] = 0.5f },
            [Element.Shade] = new Dictionary<Element, float> { [Element.Mind] = 2f, [Element.Shade] = 0.5f, [Element.Beast] = 0.5f },
            [Element.Beast] = new Dictionary<Element, float> { [Element.Mind] = 2f, [Element.Shade] = 2f, [Element.Gale] = 0.5f, [Element.Frost] = 0.5f },
            [Element.Normal] = new Dictionary<Element, float> { [Element.Stone] = 0.5f, [Element.Shade] = 0.5f },
        };

        public static float Effectiveness(Element attack, Element defender)
        {
            Dictionary<Element, float> row;
            if (chart.TryGetValue(attack, out row))
            {
                float f;
                if (row.TryGetValue(defender, out f)) return f;
            }
            return 1f;
        }

        public static float Effectiveness(Element attack, Element def1, Element def2)
        {
            if (def1 == def2) return Effectiveness(attack, def1);
            return Effectiveness(attack, def1) * Effectiveness(attack, def2);
        }

        public static string Label(float mult)
        {
            if (mult >= 2f) return "Super effective!";
            if (mult <= 0.5f && mult > 0f) return "Not very effective...";
            if (mult <= 0f) return "No effect...";
            return "";
        }

        public static string Hex(Element e)
        {
            switch (e)
            {
                case Element.Normal: return "#A8A878";
                case Element.Ember: return "#F08030";
                case Element.Tide: return "#6890F0";
                case Element.Verdant: return "#78C850";
                case Element.Volt: return "#F8D030";
                case Element.Stone: return "#B8A038";
                case Element.Gale: return "#A890F0";
                case Element.Frost: return "#98D8D8";
                case Element.Toxic: return "#A040A0";
                case Element.Mind: return "#F85888";
                case Element.Shade: return "#705898";
                case Element.Beast: return "#705848";
            }
            return "#888888";
        }
    }
}
