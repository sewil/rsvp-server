using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WvsBeta.SharedDataProvider.Templates;
using WzTools.FileSystem;

namespace WvsBeta.SharedDataProvider.Providers
{
    public class QuizProvider : TemplateProvider<byte, List<QuizData>>
    {
        public QuizProvider(WzFileSystem fileSystem) : base(fileSystem)
        {
        }

        public override IDictionary<byte, List<QuizData>> LoadAll()
        {
            return FileSystem.GetProperty("Etc", "OXQuiz.img").PropertyChildren
                .Where(property => byte.Parse(property.Name) < 8) //pages past 7 are untranslated korean in this version
                .Select(categoryNode =>
                {
                    var category = byte.Parse(categoryNode.Name);
                    var questionList = categoryNode.PropertyChildren
                        .Select(numberNode => new QuizData(
                            category,
                            short.Parse(numberNode.Name), 
                            numberNode.GetInt8("a") == 0 ? 'x' : 'o'
                        ))
                        .ToList();
                    
                    return (Category: category, Questions: questionList);
                }).ToDictionary(tuple => tuple.Category, tuple => tuple.Questions);
        }
    }
}