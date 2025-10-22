using System;
using System.Threading.Tasks;

namespace SysWeaver.LanguageIdentifier
{
    public interface ILanguageIdentifier
    {
        /// <summary>
        /// Try to identify a langauge used by some text
        /// </summary>
        /// <param name="text">The text to identify</param>
        /// <param name="userLanguge">An optional two letter ISO 639-1 language code that will have some bias for being more likely</param>
        /// <param name="userLanguageBias">How much to bias the user language [0, 1]</param>
        /// <param name="minConfidence">The minimum confidence required to return a lanmguage</param>
        /// <returns>A two letter ISO 639-1 language code of the identified language or null if the language couldn't be identified</returns>
        ValueTask<String> Identify(String text, String userLanguge = null, double userLanguageBias = 0.2, double minConfidence = 0.05);

        /// <summary>
        /// Try to identify a langauge used by some text, returns up to N number of guesses and a confidence score
        /// </summary>
        /// <param name="text">The text to identify</param>
        /// <param name="numberOfResults">Maximum number of results</param>
        /// <param name="userLanguge">An optional two letter ISO 639-1 language code that will have some bias for being more likely</param>
        /// <param name="userLanguageBias">How much to bias the user language [0, 1]</param>
        /// <returns></returns>
        ValueTask<IdentifiedLanguage[]> Identify(String text, int numberOfResults, String userLanguge = null, double userLanguageBias = 0.2);

    }

}