/***
 * 
 * RottenToMeTo.cs
 * 
 * Downloads my movie ratings from Flixster, compares them to other users' or critics' ratings, and
 * makes a nice Web page summarizing the movies I like more/less than other people.
 *
 * Uses only public information; the USER_ID string sets the target user Id.
 * 
 * Dan Morris, 2017
 * 
 * Released under the granola bar license: if you find this code useful, please bring me a granola bar.
 * 
 ***/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RottenToMeTo
{
    public class Movie
    {
        public String title = "";
        public double tomatoMeter;
        public double audienceScore;
        public double myScore;
        public double toMeToMeter_critics;
        public double toMeToMeter_audience;
        public String url = "";
        public String year;
        public Dictionary<String, String> thumbnails = new Dictionary<string, string>();

        public override string ToString()
        {
            return String.Format("{0}: tomato={1:0}, audience={2:0}, me={3:0}, tmCritics={4:0}, tmAudience={5:0}",
                title, tomatoMeter, audienceScore, myScore, toMeToMeter_critics, toMeToMeter_audience);
        }

        public Movie(String aTitle, double aTM, double aAS, double aMS, double aTMTM_critics, double aTMTM_audience, String aUrl, String aYear)
        {
            title = aTitle;
            tomatoMeter = aTM;
            audienceScore = aAS;
            myScore = aMS;
            toMeToMeter_critics = aTMTM_critics;
            toMeToMeter_audience = aTMTM_audience;
            url = aUrl;
            year = aYear;
        }
    }

    class RottenToMeTo
    {
        // How far down in the list should I go in each of the four tables?
        //
        // The four tables are:
        //
        // * My favorite movies relative to the critics
        // * My favorite movies relative to the people
        // * My least favorite movies relative to the critics
        // * My least favorite movies relative to the people
        static int nMoviesPerTable = 10;
           
        // I found this ID by logging into Flixster and clicking on my login name ("Daniel") in the upper-right
        static String USER_ID = "912123977";

        static String APISTRING = "https://www.flixster.com/api/users/[ID]/movies/ratings?scoreTypes=numeric&page=1&limit=999";

        static String OUTPUT_HTML_FILE = "index.html";
        static String TEMPLATE_HTML_FILE = "outputTemplate.html";
        static String THUMBS_DIR = "thumbs//";

        static bool defaultToFile = true;

        static void Main(string[] args)
        {
            bool readURL = false;
            String json = "";
            String filename = USER_ID + ".json";

            if (defaultToFile)
            {
                if (System.IO.File.Exists(filename))
                {
                    Console.WriteLine("Reading from file {0}...", filename);
                    json = System.IO.File.ReadAllText(filename);
                }
                else
                {
                    Console.WriteLine("File not available, falling back to http...");
                    readURL = true;
                }
            }
            if (readURL)
            {
                using (WebClient client = new WebClient())
                {
                    String url = APISTRING.Replace("[ID]", USER_ID);
                    Console.WriteLine("Fetching from {0}", url);
                    json = client.DownloadString(url);
                    File.WriteAllText(filename, json);
                }
            } // if we're reading from the Web

            json = json.Trim();

            // JObject o = JObject.Parse(json.Trim());

            // http://stackoverflow.com/questions/18192357/deserializing-json-object-array-with-json-net
            // Object o = JsonConvert.DeserializeObject<List<Dictionary<string, Dictionary<string, string>>>>(json);
            // System.Collections.Generic.Dictionary<string, string> values = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

            // http://stackoverflow.com/questions/8738031/deserializing-json-using-json-net-with-dynamic-data
            dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);

            List<Movie> allMovies = new List<Movie>();

            if (!System.IO.Directory.Exists(THUMBS_DIR))
            {
                System.IO.Directory.CreateDirectory(THUMBS_DIR);
            }                
            
            // For each movie...
            //
            // This loop is only run serially out of laziness; mostly this is just fetching thumbnails and there's 
            // no reason not to parallelize this.
            foreach (Object o in data)
            {
                String titleString = "unknown";

                try
                {
                    String s = o.ToString();
                    JObject movieObj = JObject.Parse(s.Trim());

                    // Pull out the individual fields we care about from the JSON object

                    // "id", "user", "movie", "movieid", "score", "review", others
                    JValue scoreObj = (JValue)movieObj["score"];
                    JValue reviewObj = (JValue)movieObj["review"];
                    JObject innerMovieObj = (JObject)movieObj["movie"];

                    String scoreString = (String)scoreObj.Value;
                    String reviewString = (String)reviewObj.Value;

                    // "id", "title", "year", "synopsis", "url", "vanity", "movietype", "poster", "tomatometer", "audienceScore", "mpaa", "runningTime", "dvdReleaseDate", "cast", others
                    JValue titleObj = (JValue)innerMovieObj["title"];
                    JValue yearObj = (JValue)innerMovieObj["year"];
                    JValue tomatoMeterObj = (JValue)innerMovieObj["tomatometer"];
                    JValue audienceScoreObj = (JValue)innerMovieObj["audienceScore"];

                    JValue urlObj = (JValue)innerMovieObj["url"];
                    String urlString = (String)urlObj.ToString();
                    String yearString = (String)yearObj.ToString();

                    // thumbnail, mobile, profile, detialed, 320X480
                    JObject posterObj = (JObject)innerMovieObj["poster"];

                    titleString = (String)titleObj.Value.ToString();
                    String tomatoMeterString = (String)tomatoMeterObj.Value.ToString();
                    String audienceScoreString = (String)audienceScoreObj.Value.ToString();

                    double myScore = double.Parse(scoreString);
                    double tomatoMeter = double.Parse(tomatoMeterString);
                    double audienceScore = double.Parse(audienceScoreString);

                    // This normalizes the "stars" range [0.5,5] into the range I prefer [0,100]
                    myScore = 22.222222 * myScore - 11.111111;

                    double toMeToMeter_critics = myScore - tomatoMeter;
                    double toMeToMeter_audience = myScore - audienceScore;

                    Movie m = new Movie(titleString, tomatoMeter, audienceScore, myScore, toMeToMeter_critics, toMeToMeter_audience, urlString, yearString);

                    allMovies.Add(m);

                    String[] thumbNames = new String[] { "thumbnail", "mobile", "profile", "320X480" };

                    String thumbnailString = (((JValue)(posterObj["thumbnail"])).Value).ToString();
                    String mobileString = (((JValue)(posterObj["mobile"])).Value).ToString();
                    String profileString = (((JValue)(posterObj["profile"])).Value).ToString();
                    String p320x480String = (((JValue)(posterObj["320X480"])).Value).ToString();

                    // Grab all the different thumbnails for this movie
                    for (int thumbIndex = 0; thumbIndex < thumbNames.Length; thumbIndex++)
                    {
                        String n = thumbNames[thumbIndex];
                        String url = (((JValue)(posterObj[n])).Value).ToString();

                        String imgFilename = FilenameFromUrl(url);
                        String localImgFilename = THUMBS_DIR + imgFilename;
                        if (!System.IO.File.Exists(localImgFilename))
                        {
                            Console.WriteLine("Downloading thumbnail image {0}", url);
                            DownloadRemoteImageFile(url, localImgFilename);
                        }

                        m.thumbnails[n] = url;
                    }

                    // Console.WriteLine("Parsed movie {0}", m.ToString());
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error parsing movie {0}: {1}", titleString, e.ToString());
                }

            } // foreach(Object o in data)

            /*
            Dictionary<string, object> result = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            foreach (var item in result)
                Console.WriteLine(item.Key + " " + item.Value);
            */

            Console.WriteLine("Finished deserializing {0} objects...", allMovies.Count);

            List<Movie> moviesByMyScore = allMovies.OrderByDescending(o => o.myScore).ToList();
            List<Movie> moviesByMyScore_reverse = allMovies.OrderBy(o => o.myScore).ToList();

            List<Movie> moviesByCritic = allMovies.OrderByDescending(o => o.toMeToMeter_critics).ToList();
            List<Movie> moviesByAudience = allMovies.OrderByDescending(o => o.toMeToMeter_audience).ToList();

            List<Movie> moviesByCritic_reverse = allMovies.OrderBy(o => o.toMeToMeter_critics).ToList();
            List<Movie> moviesByAudience_reverse = allMovies.OrderBy(o => o.toMeToMeter_audience).ToList();

            int nMovies = 0;

            Console.WriteLine("\n *** Top {0} movies compared to critics:", nMoviesPerTable);
            for (int i = 0; i < moviesByCritic.Count; i++) Console.WriteLine("{0}: {1}", i, moviesByCritic[i].ToString());

            Console.WriteLine("\n *** Top {0} movies compared to the audience:", nMoviesPerTable);
            for (int i = 0; i < moviesByAudience.Count; i++) Console.WriteLine("{0}: {1}", i, moviesByAudience[i].ToString());

            Console.WriteLine("\n *** Bottom {0} movies compared to critics:", nMoviesPerTable);
            for (int i = 0; i < moviesByCritic_reverse.Count; i++) Console.WriteLine("{0}: {1}", i, moviesByCritic_reverse[i].ToString());

            Console.WriteLine("\n *** Bottom {0} movies compared to the audience:", nMoviesPerTable);
            for (int i = 0; i < moviesByAudience_reverse.Count; i++) Console.WriteLine("{0}: {1}", i, moviesByAudience_reverse[i].ToString());

            Console.WriteLine("\n *** Top {0} movies by my score:", nMoviesPerTable);
            for (int i = 0; i < moviesByMyScore.Count; i++) Console.WriteLine("{0}: {1}", i, moviesByMyScore[i].ToString());

            Console.WriteLine("\n *** Bottom {0} movies by my score:", nMoviesPerTable);
            for (int i = 0; i < moviesByMyScore_reverse.Count; i++) Console.WriteLine("{0}: {1}", i, moviesByMyScore_reverse[i].ToString());

            // Trim lists, and pad out last-place ties                        
            for (nMovies = nMoviesPerTable; nMovies < allMovies.Count; nMovies++)
            {
                if (moviesByMyScore[nMovies - 1].myScore == moviesByMyScore[nMoviesPerTable - 1].myScore) nMovies++;
                else break;
            }
            moviesByMyScore.RemoveRange(nMovies, allMovies.Count - nMovies);

            for (nMovies = nMoviesPerTable; nMovies < allMovies.Count; nMovies++)
            {
                if (moviesByMyScore_reverse[nMovies - 1].myScore == moviesByMyScore_reverse[nMoviesPerTable - 1].myScore) nMovies++;
                else break;
            }
            moviesByMyScore_reverse.RemoveRange(nMovies, allMovies.Count - nMovies);

            for (nMovies = nMoviesPerTable; nMovies < allMovies.Count; nMovies++)
            {
                if (moviesByCritic[nMovies - 1].toMeToMeter_critics == moviesByCritic[nMoviesPerTable - 1].toMeToMeter_critics) nMovies++;
                else break;
            }
            moviesByCritic.RemoveRange(nMovies, allMovies.Count - nMovies);

            for (nMovies = nMoviesPerTable; nMovies < allMovies.Count; nMovies++)
            {
                if (moviesByCritic_reverse[nMovies - 1].toMeToMeter_critics == moviesByCritic_reverse[nMoviesPerTable - 1].toMeToMeter_critics) nMovies++;
                else break;
            }
            moviesByCritic_reverse.RemoveRange(nMovies, allMovies.Count - nMovies);


            for (nMovies = nMoviesPerTable; nMovies < allMovies.Count; nMovies++)
            {
                if (moviesByAudience[nMovies - 1].toMeToMeter_audience == moviesByAudience[nMoviesPerTable - 1].toMeToMeter_audience) nMovies++;
                else break;
            }
            moviesByAudience.RemoveRange(nMovies, allMovies.Count - nMovies);

            for (nMovies = nMoviesPerTable; nMovies < allMovies.Count; nMovies++)
            {
                if (moviesByAudience_reverse[nMovies - 1].toMeToMeter_audience == moviesByAudience_reverse[nMoviesPerTable - 1].toMeToMeter_audience) nMovies++;
                else break;
            }
            moviesByAudience_reverse.RemoveRange(nMovies, allMovies.Count - nMovies);
            

            /*** Now make the HTML page ***/

            try
            {
                // Read HTML template
                String[] templateLines = System.IO.File.ReadAllLines(TEMPLATE_HTML_FILE);

                System.IO.TextWriter tw = new System.IO.StreamWriter(OUTPUT_HTML_FILE, false);

                for (int inputLineIndex = 0; inputLineIndex < templateLines.Length; inputLineIndex++)
                {

                    String line = templateLines[inputLineIndex];
                    if (line.Trim().Equals("$CONTENT"))
                    {
                        // WriteMovieTable(String title, List<Movie> movies, TextWriter tw, bool audience)
                        tw.WriteLine("<a name=\"#topToCritics\"/>");
                        tw.WriteLine("<a name=\"#topToAudience\"/>");

                        tw.WriteLine("<table class=\"metaTable\"><tr class=\"metaTr\">");
                        tw.WriteLine("<td class=\"metaTd\">");
                        WriteMovieTable("Movies I liked more than the critics", moviesByCritic, tw, false);
                        tw.WriteLine("</td>");
                        tw.WriteLine("<td class=\"metaTd\">");
                        WriteMovieTable("Movies I liked more than the audience", moviesByAudience, tw, true);
                        tw.WriteLine("</td>");
                        tw.WriteLine("</tr></table>");

                        tw.WriteLine("<a name=\"#bottomToCritics\"/>");
                        tw.WriteLine("<a name=\"#bottomToAudience\"/>");

                        tw.WriteLine("<table class=\"metaTable\"><tr class=\"metaTr\">");
                        tw.WriteLine("<td class=\"metaTd\">"); 
                        WriteMovieTable("Movies I liked less than the critics", moviesByCritic_reverse, tw, false);
                        tw.WriteLine("</td>");
                        tw.WriteLine("<td class=\"metaTd\">"); 
                        WriteMovieTable("Movies I liked less than the audience", moviesByAudience_reverse, tw, true);
                        tw.WriteLine("</td>");
                        tw.WriteLine("</tr></table>");

                    }
                    else
                    {
                        tw.WriteLine(line);
                    }

                } // for each input line

                tw.Close();

            } // try

            catch (Exception e)
            {
                Console.WriteLine("File i/o error {0}", e.ToString());
            }

            Console.WriteLine("Finished writing html output...");

        } // Main()

        private static String FilenameFromUrl(String url)
        {
            Uri uri = new Uri(url);
            return System.IO.Path.GetFileName(uri.LocalPath);                        
        }

        // http://stackoverflow.com/questions/3615800/download-image-from-the-site-in-net-c
        private static void DownloadRemoteImageFile(string uri, string fileName)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            // Check that the remote file was found. The ContentType
            // check is performed since a request for a non-existent
            // image file might be redirected to a 404-page, which would
            // yield the StatusCode "OK", even though the image was not
            // found.
            if ((response.StatusCode == HttpStatusCode.OK ||
                response.StatusCode == HttpStatusCode.Moved ||
                response.StatusCode == HttpStatusCode.Redirect) &&
                response.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
            {

                // if the remote file was found, download oit
                using (Stream inputStream = response.GetResponseStream())
                using (Stream outputStream = File.OpenWrite(fileName))
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead;
                    do
                    {
                        bytesRead = inputStream.Read(buffer, 0, buffer.Length);
                        outputStream.Write(buffer, 0, bytesRead);
                    } while (bytesRead != 0);
                }
            }
        }

        public static void WriteMovieTable(String title, List<Movie> movies, TextWriter tw, bool audience)
        {

            tw.WriteLine("<div class=\"movieTableDiv\">");

            tw.WriteLine("<p class=\"movieTableTitleP\">{0}</p>", title);

            tw.WriteLine("<table class=\"movieTable\">");

            int rank = 1;
            foreach(Movie m in movies)
            {
                tw.WriteLine("<tr class=\"movieTR\">");

                // N, thumb, title, tomato, me, tometometer

                String url = m.url;
                double rtScore = m.audienceScore;
                double toMetoMeter = m.toMeToMeter_audience;
                String sourceString = "Audience";

                if (!audience)
                {
                    sourceString = "Critics";
                    rtScore = m.tomatoMeter;
                    toMetoMeter = m.toMeToMeter_critics;
                }

                String imageFileName = FilenameFromUrl(m.thumbnails["mobile"]);

                String localImgFilename = THUMBS_DIR + imageFileName;

                tw.Write("<td class=\"movieRankTD\"><p class=\"movieNP\">{0}</p></td>",rank);
                tw.Write("<td class=\"movieThumbTD\"><p class=\"movieThumbP\"><a href=\"{0}\" class=\"movieThumbHref\"><img src=\"{1}\" class=\"movieThumbImg\"/></a></p></td>", url, localImgFilename);
                tw.Write("<td class=\"movieContentTD\">");
                tw.Write("<p class=\"movieTitleP\"><a href=\"{0}\" class=\"movieTitleHref\">{1} ({2})</a></p>", url, m.title, m.year);
                tw.Write("<p class=\"movieScoresP\"><span class=\"movieRtScoreSpan\">{2}: {0}%</span>, <span class=\"movieMeScoreSpan\">Me: {1}%</span>", (int)(Math.Round(rtScore)), (int)(Math.Round(m.myScore)), sourceString);
                String signString = "";
                if (toMetoMeter >= 0) signString = "+";

                tw.Write("<p class=\"movieTometoP\">ToMetoMeter: <span class=\"movieTometoMeterScore\">{0}{1}%</span></p>", signString, (int)(Math.Round(toMetoMeter)));
                tw.Write("</td>");
                tw.WriteLine("</tr>");
                rank++;
                if (rank > nMoviesPerTable) rank = nMoviesPerTable;
            }
            tw.WriteLine("</table>");

            tw.WriteLine("</div>");

        } // public static void WriteMovieTable(String title, List<Movie> movies, TextWriter tw, bool audience)


        // http://www.cshandler.com/2013/09/deserialize-list-of-json-objects-as.html            
        public static List<string> InvalidJsonElements;

        public static IList<T> DeserializeToList<T>(string jsonString)
        {           
            InvalidJsonElements = null;
            var array = JArray.Parse(jsonString);
            IList<T> objectsList = new List<T>();

            foreach (var item in array)
            {
                try
                {
                    // CorrectElements
                    objectsList.Add(item.ToObject<T>());
                }
                catch (Exception)
                {
                    InvalidJsonElements = InvalidJsonElements ?? new List<string>();
                    InvalidJsonElements.Add(item.ToString());
                }
            }

            return objectsList;

        } // public static IList<T> DeserializeToList<T>(string jsonString)

    } // Program

} // namespace
