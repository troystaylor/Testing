using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

public class Script : ScriptBase
{
    public override async Task<HttpResponseMessage> ExecuteAsync()
    {
        var _logger = this.Context.Logger;

        var authURL = "https://api.ilovepdf.com/v1/auth";
        var startURL = "https://api.ilovepdf.com/v1/start/{tool}";
        var uploadURL = "https://{server}/v1/upload";
        var processURL = "https://{server}/v1/process";
        var downloadURL = "https://{server}/v1/download/{task}";

        if (this.Context.OperationId == "PDFGet" || this.Context.OperationId == "ImageGet")
        {
            var authorizationHeader = this.Context.Request.Headers.GetValues("public_key").FirstOrDefault();
            var toolHeader = this.Context.Request.Headers.GetValues("tool").FirstOrDefault();
            var cloudFileHeader = this.Context.Request.Headers.GetValues("cloud_file").FirstOrDefault();
            var cloudFileUri = new Uri(cloudFileHeader);
            var filename = System.IO.Path.GetFileName(cloudFileUri.LocalPath);

            var authRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(authURL))
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "public_key", authorizationHeader }
                })
            };
            HttpResponseMessage authResponse = await this.Context.SendAsync(authRequest, this.CancellationToken);
            if (!authResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Authentication failed.");
                return authResponse;
            }
            var jsonResponse = JObject.Parse(await authResponse.Content.ReadAsStringAsync());
            jsonResponse.TryGetValue("token", out JToken signedToken);
            var signedBearerToken = new AuthenticationHeaderValue("Bearer", signedToken.ToString());

            var startRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(startURL.Replace("{tool}", toolHeader)))
            {
                Headers = { Authorization = signedBearerToken }
            };
            HttpResponseMessage startResponse = await this.Context.SendAsync(startRequest, this.CancellationToken);
            if (!startResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Start request failed.");
                return startResponse;
            }
            var startJsonResponse = JObject.Parse(await startResponse.Content.ReadAsStringAsync());
            startJsonResponse.TryGetValue("server", out JToken serverToken);
            startJsonResponse.TryGetValue("task", out JToken taskToken);

            var uploadRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(uploadURL.Replace("{server}", serverToken.ToString())))
            {
                Headers = { Authorization = signedBearerToken },
                Content = new MultipartFormDataContent
                {
                    { new StringContent(taskToken.ToString()), "task" },
                    { new StringContent(cloudFileHeader), "cloud_file" }
                }
            };
            HttpResponseMessage uploadResponse = await this.Context.SendAsync(uploadRequest, this.CancellationToken);
            if (!uploadResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Upload request failed.");
                return uploadResponse;
            }
            var uploadJsonResponse = JObject.Parse(await uploadResponse.Content.ReadAsStringAsync());
            uploadJsonResponse.TryGetValue("server_filename", out JToken serverFilenameID);

            var processRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(processURL.Replace("{server}", serverToken.ToString())))
            {
                Headers = { Authorization = signedBearerToken },
                Content = new StringContent(new JObject
                {
                    { "task", taskToken },
                    { "tool", toolHeader },
                    { "files", new JArray(new JObject
                        {
                            { "server_filename", serverFilenameID },
                            { "filename", filename }
                        })
                    }
                }.ToString(), Encoding.UTF8, "application/json")
            };
            HttpResponseMessage processResponse = await this.Context.SendAsync(processRequest, this.CancellationToken);
            if (!processResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Process request failed.");
                return processResponse;
            }

            var downloadRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(downloadURL.Replace("{server}", serverToken.ToString()).Replace("{task}", taskToken.ToString())))
            {
                Headers = { Authorization = signedBearerToken }
            };
            HttpResponseMessage downloadResponse = await this.Context.SendAsync(downloadRequest, this.CancellationToken);
            if (!downloadResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Download request failed.");
                return downloadResponse;
            }
            return downloadResponse;
        }

        if (this.Context.OperationId == "SchemaPDFGet")
        {
            var queryParameters = System.Web.HttpUtility.ParseQueryString(this.Context.Request.RequestUri.Query);
            var toolQuery = queryParameters["tool"];

            var toolSchemas = new Dictionary<string, JObject>
            {
                { "compress", new JObject {
                    { "items", new JObject {
                        { "type", "object" },
                        { "properties", new JObject {
                            { "compression_level", new JObject {
                                { "type", "string" },
                                { "description", "The compression level." },
                                { "title", "Compression Level" },
                                { "enum", new JArray { "extreme", "recommended", "low" } },
                                { "default", "recommended" }
                            } }
                        } }
                    } }
                } },
                { "extract", new JObject {
                    { "items", new JObject {
                        { "type", "object" },
                        { "properties", new JObject {
                            { "detailed", new JObject {
                                { "type", "boolean" },
                                { "description", "Includes the following PDF properties separated by a comma: PageNo, XPos, YPos, Width, FontName, FontSize, Length and Text." },
                                { "title", "Detailed" }
                            } }
                        } },
                        { "required", new JArray { "detailed" } } // Required property at the object level
                    } }
                } },
                { "imagepdf", new JObject {
                    { "items", new JObject {
                        { "type", "object" },
                        { "properties", new JObject {
                            { "orientation", new JObject {
                                { "type", "string" },
                                { "description", "The orientation." },
                                { "title", "Orientation" },
                                { "enum", new JArray { "portrait", "landscape" } },
                                { "default", "portrait" }
                            } },
                            { "margin", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "Define a margin in pixels for the image in the output PDF." },
                                { "title", "Margin" },
                                { "default", "1" }
                            } },
                            { "pagesize", new JObject {
                                { "type", "string" },
                                { "description", "The page size of the output PDF." },
                                { "title", "Page Size" },
                                { "enum", new JArray { "fit", "a4", "letter" } },
                                { "default", "fit" }
                            } },
                            { "merge_after", new JObject {
                                { "type", "boolean" },
                                { "description", "Serve all converted images in a unique PDF if true. If false, serve every image into a separate PDF." },
                                { "title", "Merge After" },
                                { "default", "true" }
                            } }
                        } }
                    } }
                } },
                { "pagenumber", new JObject {
                    { "items", new JObject {
                        { "type", "object" },
                        { "properties", new JObject {
                            { "facing_pages", new JObject {
                                { "type", "boolean" },
                                { "description", "Define it to true if the PDF is in facing page mode." },
                                { "title", "Facing Pages" },
                                { "default", "false" }
                            } },
                            { "first_cover", new JObject {
                                { "type", "boolean" },
                                { "description", "If the first page is a cover page, it will not be numbered." },
                                { "title", "First Cover" },
                                { "default", "false" }
                            } },
                            { "compression_level", new JObject {
                                { "type", "string" },
                                { "description", "The compression level." },
                                { "title", "Compression Level" },
                                { "enum", new JArray { "extreme", "recommended", "low" } },
                                { "default", "recommended" }
                            } },
                            { "pages", new JObject {
                                { "type", "string" },
                                { "description", "The pages to be numbered. Accepted formats: 'all', '3-end', '1,3,4-9', '-2-end', '3-234'" },
                                { "title", "Pages to Number" },
                                { "default", "all" }
                            } },
                            { "starting_number", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "Start page numbering with this number." },
                                { "title", "Starting Number" },
                                { "default", "1" }
                            } },
                            { "vertical_position", new JObject {
                                { "type", "string" },
                                { "description", "Define if page number will be at top or bottom." },
                                { "title", "Vertical Position" },
                                { "enum", new JArray { "bottom", "top" } },
                                { "default", "bottom" }
                            } },
                            { "horizontal_position", new JObject {
                                { "type", "string" },
                                { "description", "Allows you to position the number on the left, in the center or on the right of the page. However, if the parameter facing_pages is set to true, facing pages will have their page numbers positioned symmetrically, on the left and the right." },
                                { "title", "Horizontal Position" },
                                { "enum", new JArray { "center", "left", "right" } },
                                { "default", "center" }
                            } },
                            { "vertical_position_adjustment", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "Adjust the number of pixels displaced from the standard vertical_position. It accepts positive and negative values." },
                                { "title", "Vertical Position Adjustment" }
                            } },
                            { "horizontal_position_adjustment", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "Adjust the number of pixels displaced from the standard horizontal_position. It accepts positive and negative values." },
                                { "title", "Horizontal Position Adjustment" }
                            } },
                            { "font_family", new JObject {
                                { "type", "string" },
                                { "description", "The font family." },
                                { "title", "Font Family" },
                                { "enum", new JArray { "Arial Unicode MS", "Arial", "Verdana", "Courier", "Times New Roman", "Comic Sans MS", "WenQuanYi Zen Hei", "Lohit Marathi" } },
                                { "default", "Arial Unicode MS" }
                            } },
                            { "font_size", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "The font size." },
                                { "title", "Font Size" },
                                { "default", "14" }
                            } },
                            { "font_color", new JObject {
                                { "type", "string" },
                                { "description", "The font hexadecimal color." },
                                { "title", "Font Color" },
                                { "default", "#000000" }
                            } },
                            { "text", new JObject {
                                { "type", "string" },
                                { "description", "To define text in addition to the number, use {n} for current page number and {p} for total number of pages for the file. Example: 'Page {n} of {p}'." },
                                { "title", "Additional Text" },
                                { "default", "{n}" }
                            } }
                        } }
                    } }
                } },
                { "pdfa", new JObject {
                    { "items", new JObject {
                        { "type", "object" },
                        { "properties", new JObject {
                            { "conformance", new JObject {
                                { "type", "string" },
                                { "description", "Sets the PDF/A conformance level." },
                                { "title", "Conformance" },
                                { "enum", new JArray { "pdfa-2b", "pdfa-1b", "pdfa-1a", "pdfa-2b", "pdfa-2u", "pdfa-2a", "pdfa-3b", "pdfa-3u", "pdfa-3a" } },
                                { "default", "pdfa-2b" }
                            } },
                            { "allow_downgrade", new JObject {
                                { "type", "boolean" },
                                { "description", "Allows conformance downgrade in case of conversion error." },
                                { "title", "Allow Downgrade" },
                                { "default", "true" }
                            } }
                        } }
                    } }
                } },
                { "pdfjpg", new JObject {
                    { "items", new JObject {
                        { "type", "object" },
                        { "properties", new JObject {
                            { "pdfjpg_mode", new JObject {
                                { "type", "string" },
                                { "description", "Sets the mode. Accepted values: 'pages' = Convert every PDF page to a JPG image, 'extract' = extract all PDF's embedded images to separate JPG images." },
                                { "title", "PDF to JPG Mode" },
                                { "enum", new JArray { "pages", "extract" } },
                                { "default", "pages" }
                            } }
                        } }
                    } }
                } },
                { "pdfocr", new JObject {
                    { "items", new JObject {
                        { "type", "object" },
                        { "properties", new JObject {
                            { "ocr_languages", new JObject {
                                { "type", "array" },
                                { "items", new JObject {
                                    { "type", "string" },
                                    { "enum", new JArray { "eng", "afr", "amh", "ara", "asm", "aze", "aze_cyrl", "bel", "ben", "bod", "bos", "bre", "bul", "cat", "ceb", "ces", "chi_sim", "chi_tra", "chr", "cos", "cym", "dan", "deu", "deu_latf", "dzo", "ell", "enm", "epo", "equ", "est", "eus", "fao", "fas", "fil", "fin", "fra", "frm", "fry", "gla", "gle", "glg", "grc", "guj", "hat", "heb", "hin", "hrv", "hun", "hye", "iku", "ind", "isl", "ita", "ita_old", "jav", "jpn", "kan", "kat", "kat_old", "kaz", "khm", "kir", "kmr", "kor", "kor_vert", "lao", "lat", "lav", "lit", "ltz", "mal", "mar", "mkd", "mlt", "mon", "mri", "msa", "mya", "nep", "nld", "nor", "oci", "ori", "pan", "pol", "por", "pus", "que", "ron", "rus", "san", "sin", "slk", "slv", "snd", "spa", "spa_old", "sqi", "srp", "srp_latn", "sun", "swa", "swe", "syr", "tam", "tat", "tel", "tgk", "tgl", "tha", "tir", "ton", "tur", "uig", "ukr", "urd", "uzb", "uzb_cyrl", "vie", "yid", "yor" } },
                                    { "default", "eng" }
                                } },
                                { "description", "The OCR languages to apply" },
                                { "title", "Languages" }
                            } }
                        } }
                    } }
                } },
                { "protect", new JObject {
                    { "items", new JObject {
                        { "type", "object" },
                        { "properties", new JObject {
                            { "password", new JObject {
                                { "type", "string" },
                                { "description", "The password with which the PDF file will be encrypted." },
                                { "title", "Password" }
                            } }
                        } }
                    } }
                } },
                { "split", new JObject {
                    { "items", new JObject {
                        { "type", "object" },
                        { "properties", new JObject {
                            { "split_mode", new JObject {
                                { "type", "string" },
                                { "description", "The split mode." },
                                { "title", "Split Mode" },
                                { "enum", new JArray { "ranges", "fixed_ranges", "remove_pages" } },
                                { "default", "pages" }
                            } },
                            { "ranges", new JObject {
                                { "type", "string" },
                                { "description", "The page ranges to split the files. Every range will be saved as a different PDF file. Example format: '1,5,10-14'." },
                                { "title", "Ranges" }
                            } },
                            { "fixed_range", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "The page ranges to split the files. Every range will be saved as a different PDF file." },
                                { "title", "Fixed Range" }
                            } },
                            { "remove_pages", new JObject {
                                { "type", "string" },
                                { "description", "The pages to remove from a PDF. Accepted format: '1,4,8-12,16'." },
                                { "title", "Remove Pages" }
                            } },
                            { "merge_after", new JObject {
                                { "type", "boolean" },
                                { "description", "Merge all ranges after being split. This param takes effect only when ranges is mode." },
                                { "title", "Merge After" },
                                { "default", "false" }
                            } }
                        } }
                    } }
                } },
                { "validatepdfa", new JObject {
                    { "items", new JObject {
                        { "type", "object" },
                        { "properties", new JObject {
                            { "conformance", new JObject {
                                { "type", "string" },
                                { "description", "Sets the PDF/A conformance level." },
                                { "title", "Conformance" },
                                { "enum", new JArray { "pdfa-2b", "pdfa-1b", "pdfa-1a", "pdfa-2b", "pdfa-2u", "pdfa-2a", "pdfa-3b", "pdfa-3u", "pdfa-3a" } },
                                { "default", "pdfa-2b" }
                            } },
                            { "allow_downgrade", new JObject {
                                { "type", "boolean" },
                                { "description", "Allows conformance downgrade in case of conversion error." },
                                { "title", "Allow Downgrade" },
                                { "default", "true" }
                            } }
                        } }
                    } }
                } },
                { "watermark", new JObject {
                    { "items", new JObject {
                        { "type", "object" },
                        { "properties", new JObject {
                            { "mode", new JObject {
                                { "type", "string" },
                                { "description", "The watermark mode." },
                                { "title", "Watermark Mode" },
                                { "enum", new JArray { "text", "image" } },
                                { "default", "text" }
                            } },
                            { "text", new JObject {
                                { "type", "string" },
                                { "description", "The text to be stamped. Required if mode is text." },
                                { "title", "Text" }
                            } },
                            { "image", new JObject {
                                { "type", "string" },
                                { "description", "The image to be stamped. The stamped image must be uploaded with the Upload action. This image parameter must refer to the server_filename (JPG or PNG) to stamp. Required if mode is image." },
                                { "title", "Image" }
                            } },
                            { "pages", new JObject {
                                { "type", "string" },
                                { "description", "The pages to be stamped. Accepted formats: 'all', '3-end', '1,3,4-9', '-2-end', '3-234'." },
                                { "title", "Pages" },
                                { "default", "all" }
                            } },
                            { "vertical_position", new JObject {
                                { "type", "string" },
                                { "description", "Define if page number will be at top or bottom." },
                                { "title", "Vertical Position" },
                                { "enum", new JArray { "bottom", "top" } },
                                { "default", "bottom" }
                            } },
                            { "horizontal_position", new JObject {
                                { "type", "string" },
                                { "description", "Allows you to position the number on the left, in the center or on the right of the page. However, if the parameter facing_pages is set to true, facing pages will have their page numbers positioned symmetrically, on the left and the right." },
                                { "title", "Horizontal Position" },
                                { "enum", new JArray { "center", "left", "right" } },
                                { "default", "center" }
                            } },
                            { "vertical_position_adjustment", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "Adjust the number of pixels displaced from the standard vertical_position. It accepts positive and negative values." },
                                { "title", "Vertical Position Adjustment" }
                            } },
                            { "horizontal_position_adjustment", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "Adjust the number of pixels displaced from the standard horizontal_position. It accepts positive and negative values." },
                                { "title", "Horizontal Position Adjustment" }
                            } },
                            { "mosaic", new JObject {
                                { "type", "boolean" },
                                { "description", "If true, this value overrides all position parameters and stamps the image or text 9 times per page." },
                                { "title", "Mosaic" },
                                { "default", "false" }
                            } },
                            { "rotation", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "The angle of rotation. Accepted range: 0-360." },
                                { "title", "Rotation" }
                            } },
                            { "font_family", new JObject {
                                { "type", "string" },
                                { "description", "The font family." },
                                { "title", "Font Family" },
                                { "enum", new JArray { "Arial Unicode MS", "Arial", "Verdana", "Courier", "Times New Roman", "Comic Sans MS", "WenQuanYi Zen Hei", "Lohit Marathi" } },
                                { "default", "Arial Unicode MS" }
                            } },
                            { "font_style", new JObject {
                                { "type", "string" },
                                { "description", "The font style." },
                                { "title", "Font Style" },
                                { "enum", new JArray { "null", "Bold", "Italic" } },
                                { "default", "null" }
                            } },
                            { "font_size", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "The font size." },
                                { "title", "Font Size" },
                                { "default", "14" }
                            } },
                            { "font_color", new JObject {
                                { "type", "string" },
                                { "description", "The font hexadecimal color." },
                                { "title", "Font Color" },
                                { "default", "#000000" }
                            } },
                            { "transparency", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "The percentage of opacity for stamping text or image. Accepted integer range 1-100." },
                                { "title", "Transparency" },
                                { "default", "100" }
                            } },
                            { "layer", new JObject {
                                { "type", "string" },
                                { "description", "The layer above or below the content." },
                                { "title", "Layer" },
                                { "enum", new JArray { "above", "below" } },
                                { "default", "above" }
                            } }
                        } }
                    } }
                } }
            };

            if (toolSchemas.TryGetValue(toolQuery, out JObject schema))
            {
                var jsonResponse = new JObject(schema);
                _logger.LogInformation("SchemaGet JSON Response: " + jsonResponse.ToString());

                var responseMessage = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResponse.ToString(), Encoding.UTF8, "application/json")
                };

                return responseMessage;
            } 
            else
            {
                var toolResponse = new JObject
                {
                    { toolQuery, "Tool has no additional request parameters." }
                };

                var toolMessage = new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                {
                    Content = new StringContent(toolResponse.ToString(), Encoding.UTF8, "application/json")
                };

                return toolMessage;
            }
        }

        if (this.Context.OperationId == "SchemaImageGet")
        {
            var queryParameters = System.Web.HttpUtility.ParseQueryString(this.Context.Request.RequestUri.Query);
            var toolQuery = queryParameters["tool"];

            var toolSchemas = new Dictionary<string, JObject>
            {
                { "compressimage", new JObject {
                    { "items", new JObject {
                        { "type", "object" },
                        { "properties", new JObject {
                            { "compression_level", new JObject {
                                { "type", "string" },
                                { "description", "The compression level." },
                                { "title", "Compression Level" },
                                { "enum", new JArray { "extreme", "recommended", "low" } },
                                { "default", "recommended" }
                            } }
                        } }
                    } }
                } },
                { "cropimage", new JObject {
                    { "items", new JObject {
                        { "type", "object" },
                        { "properties", new JObject {
                            { "width", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "The width in pixels of the area to crop." },
                                { "title", "Width" }
                            } },
                            { "height", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "The height in pixels of the area to crop." },
                                { "title", "Height" }
                            } },
                            { "x", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "The horizontal point where to start to crop." },
                                { "title", "X" }
                            } },
                            { "y", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "The vertical point where to start to crop." },
                                { "title", "Y" }
                            } }
                        } },
                        { "required", new JArray { "width", "height" } }
                    } }
                } },
                { "convertimage", new JObject {
                    { "items", new JObject {
                        { "type", "object" },
                        { "properties", new JObject {
                            { "to", new JObject {
                                { "type", "string" },
                                { "description", "The format to convert to.  Convert to jpg can be (almost) from any image format. Convert to png and gif can be only from jpg images." },
                                { "title", "Convert To" },
                                { "enum", new JArray { "jpg", "png", "gif", "gif_animation", "heic" } },
                                { "default", "jpg" }
                            } }
                        } }
                    } }
                } },
                { "resizeimage", new JObject {
                    { "items", new JObject {
                        { "type", "object" },
                        { "properties", new JObject {
                            { "resize_mode", new JObject {
                                { "type", "string" },
                                { "description", "The resize mode." },
                                { "title", "Resize Mode" },
                                { "enum", new JArray { "pixels", "percentage" } },
                                { "default", "pixels" }
                            } },
                            { "pixels_width", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "The width in pixels of the resized image. Required if mode is pixels." },
                                { "title", "Pixels Width" }
                            } },
                            { "pixels_height", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "The height in pixels of the resized image. Required if mode is pixels." },
                                { "title", "Pixels Height" }
                            } },
                            { "percentage", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "The percentage value to resize. Required if mode is percentage." },
                                { "title", "Percentage" }
                            } },
                            { "maintain_ratio", new JObject {
                                { "type", "boolean" },
                                { "description", "If set as true, resize will keep aspect ratio and images will not be distort." },
                                { "title", "Maintain Ratio" },
                                { "default", "true" }
                            } },
                            { "no_enlarge_if_smaller", new JObject {
                                { "type", "boolean" },
                                { "description", "Controls if the image can be bigger than the original on resize." },
                                { "title", "No Enlarge If Smaller" },
                                { "default", "true" }
                            } }
                        } }
                    } }
                } },
                { "upscaleimage", new JObject {
                    { "items", new JObject {
                        { "type", "object" },
                        { "properties", new JObject {
                            { "multiplier", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "Upscale multiplier. Accepted values: '2', '4'." },
                                { "title", "Multiplier" }
                            } }
                        } }
                    } }
                } },
                { "watermarkimage", new JObject {
                    { "items", new JObject {
                        { "type", "object" },
                        { "properties", new JObject {
                            { "mode", new JObject {
                                { "type", "string" },
                                { "description", "The watermark mode." },
                                { "title", "Watermark Mode" },
                                { "enum", new JArray { "text", "image" } },
                                { "default", "text" }
                            } },
                            { "text", new JObject {
                                { "type", "string" },
                                { "description", "The text to be stamped. Required if mode is text." },
                                { "title", "Text" }
                            } },
                            { "image", new JObject {
                                { "type", "string" },
                                { "description", "The image to be stamped. The stamped image must be uploaded with the Upload action. This image parameter must refer to the server_filename (JPG or PNG) to stamp. Required if mode is image." },
                                { "title", "Image" }
                            } },
                            { "pages", new JObject {
                                { "type", "string" },
                                { "description", "The pages to be stamped. Accepted formats: 'all', '3-end', '1,3,4-9', '-2-end', '3-234'." },
                                { "title", "Pages" },
                                { "default", "all" }
                            } },
                            { "vertical_position", new JObject {
                                { "type", "string" },
                                { "description", "Define if page number will be at top or bottom." },
                                { "title", "Vertical Position" },
                                { "enum", new JArray { "bottom", "top" } },
                                { "default", "bottom" }
                            } },
                            { "horizontal_position", new JObject {
                                { "type", "string" },
                                { "description", "Allows you to position the number on the left, in the center or on the right of the page. However, if the parameter facing_pages is set to true, facing pages will have their page numbers positioned symmetrically, on the left and the right." },
                                { "title", "Horizontal Position" },
                                { "enum", new JArray { "center", "left", "right" } },
                                { "default", "center" }
                            } },
                            { "vertical_position_adjustment", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "Adjust the number of pixels displaced from the standard vertical_position. It accepts positive and negative values." },
                                { "title", "Vertical Position Adjustment" }
                            } },
                            { "horizontal_position_adjustment", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "Adjust the number of pixels displaced from the standard horizontal_position. It accepts positive and negative values." },
                                { "title", "Horizontal Position Adjustment" }
                            } },
                            { "mosaic", new JObject {
                                { "type", "boolean" },
                                { "description", "If true, this value overrides all position parameters and stamps the image or text 9 times per page." },
                                { "title", "Mosaic" },
                                { "default", "false" }
                            } },
                            { "rotation", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "The angle of rotation. Accepted range: 0-360." },
                                { "title", "Rotation" }
                            } },
                            { "font_family", new JObject {
                                { "type", "string" },
                                { "description", "The font family." },
                                { "title", "Font Family" },
                                { "enum", new JArray { "Arial Unicode MS", "Arial", "Verdana", "Courier", "Times New Roman", "Comic Sans MS", "WenQuanYi Zen Hei", "Lohit Marathi" } },
                                { "default", "Arial Unicode MS" }
                            } },
                            { "font_style", new JObject {
                                { "type", "string" },
                                { "description", "The font style." },
                                { "title", "Font Style" },
                                { "enum", new JArray { "null", "Bold", "Italic" } },
                                { "default", "null" }
                            } },
                            { "font_size", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "The font size." },
                                { "title", "Font Size" },
                                { "default", "14" }
                            } },
                            { "font_color", new JObject {
                                { "type", "string" },
                                { "description", "The font hexadecimal color." },
                                { "title", "Font Color" },
                                { "default", "#000000" }
                            } },
                            { "transparency", new JObject {
                                { "type", "integer" },
                                { "format", "int32" },
                                { "description", "The percentage of opacity for stamping text or image. Accepted integer range 1-100." },
                                { "title", "Transparency" },
                                { "default", "100" }
                            } },
                            { "layer", new JObject {
                                { "type", "string" },
                                { "description", "The layer above or below the content." },
                                { "title", "Layer" },
                                { "enum", new JArray { "above", "below" } },
                                { "default", "above" }
                            } }
                        } }
                    } }
                } }
            };

            if (toolSchemas.TryGetValue(toolQuery, out JObject schema))
            {
                var jsonResponse = new JObject(schema);
                _logger.LogInformation("SchemaGet JSON Response: " + jsonResponse.ToString());

                var responseMessage = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResponse.ToString(), Encoding.UTF8, "application/json")
                };

                return responseMessage;
            } 
            else
            {
                var toolResponse = new JObject
                {
                    { toolQuery, "Tool has no additional request parameters." }
                };

                var toolMessage = new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                {
                    Content = new StringContent(toolResponse.ToString(), Encoding.UTF8, "application/json")
                };

                return toolMessage;
            }
        }

        var response = await this.Context.SendAsync(this.Context.Request, this.CancellationToken).ConfigureAwait(false);
        return response;
    }
}
