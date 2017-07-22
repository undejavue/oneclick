﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClassLibrary.Models;

namespace ClassLibrary.SourceGenerator
{
    /// <summary>
    /// Класс генерации исходного кода для ПЛК
    /// </summary>
    public class SourceGenerator
    {
        private const string Copyright = "//--- Generated by OneClick Automation SourceGenerator v.3.0 ---";
        private const string CloseOb = "END_ORGANIZATION_BLOCK";
        private const string CloseFc = "END_FUNCTION";
        private const string TemplatesFolder = "CodeTemplates";


        public string Rootdir { get; set; }

        public List<CategoryModel> Categories { get; set; }

        public List<DataBlockModel> UniqDBlist { get; set; }

        public SourceGenerator()
        {
            Categories = new List<CategoryModel>();
            UniqDBlist = new List<DataBlockModel>();
        }

        public SourceGenerator(IEnumerable<CategoryModel> categoriesList)
        {
            Categories = new List<CategoryModel>(categoriesList);
            UniqDBlist = new List<DataBlockModel>();
            GenerateUniqDbList();
        }

        public bool IsEmptyCategories()
        {
            var isEmpty = !(this.Categories.Count > 0);
            return isEmpty;
        }

        public bool IsEmptyDBlist()
        {
            var isEmpty = !(this.UniqDBlist.Count > 0);
            return isEmpty;
        }

        /// <summary>
        /// Создание файлов с текстами исходного кода для
        /// программы ПЛК
        /// </summary>
        public async Task PrintAllSourcesToFiles(string dir, CancellationToken token)
        {
            Rootdir = dir + "\\";
            var filename = Rootdir;

            await Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(Rootdir);

                    foreach (var cat in Categories)
                    {
                        cat.SortCollectionByCodename();
                        if (cat.S7Items.Count > 0)
                        {
                            filename = Rootdir + "STL_" + cat.FCname + ".txt";
                            PrintListToFile(filename, PrintPeripheryForCategory(cat));
                        }
                    }

                    filename = Rootdir + "STL_DataBlocks.txt";
                    PrintListToFile(filename, GenerateStlDataBlocks());

                    filename = Rootdir + "SCL_ProcessingSensorsLogic.txt";
                    string[] s = { "SNS", "SNL", "SNC" };
                    PrintListToFile(filename, GenerateSclFcLogic(s));

                    string[] d = { "DRV", "PMP", "VLV" };
                    filename = Rootdir + "SCL_ProcessingDevicesLogic.txt";
                    PrintListToFile(filename, GenerateSclFcLogic(d));

                    filename = Rootdir + "STL_GroupControl.txt";
                    PrintListToFile(filename, GenerateStlGroupControl());

                    filename = Rootdir + "STL_OB35.txt";
                    PrintListToFile(filename, GenerateStlOb35Code());

                    filename = Rootdir + "STL_Logic_SNC.txt";
                    PrintListToFile(filename, GenerateStlLogicSnc(69));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }         

            }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Генерация полей с текстом исходного кода для всех элементов коллекции
        /// </summary>
        public async Task<bool> SetPeripheryFields(CancellationToken token)
        {
            if (IsEmptyCategories()) return false;

            await Task.Run(() =>
            {
                foreach (var cat in Categories)
                {
                    foreach (var el in cat.S7Items)
                    {
                        foreach (var s in GetPeripheryCode(el))
                        {
                            var e = new BaseEntityModel { Name = s };
                            el.PeripheryCode.Add(e);
                        }

                        if (el.DeviceType.Equals("B")) el.DeviceTag = "I_on";
                    }
                }
            }, token);

            return true;
        }

        /// <summary>
        /// Генерация текста исходного кода опроса периферии
        /// для программы ПЛК для заданной категории сигналов
        /// </summary>
        private List<string> PrintPeripheryForCategory(CategoryModel cat)
        {
            var buffer = new List<string>();

            buffer.AddRange(SourceFCopen(cat.FCname));

            var s = "";

            foreach (var el in cat.S7Items)
            {
                if (!s.Equals(el.Codename))
                {
                    s = el.Codename;
                    buffer.Add("NETWORK");
                    buffer.Add("TITLE = " + el.Codename);
                }

                buffer.AddRange(el.PeripheryCode.Select(p => p.Name).ToList());
                buffer.Add("");
                buffer.Add("");
            }

            buffer.AddRange(SourceFCclose());

            return buffer;
        }


        public List<string> PrintPeripheryForCategory(string categoryName)
        {
            var buffer = new List<string>();

            foreach (var cat in Categories)
            {
                if (cat.Name.Equals(categoryName))
                {
                    buffer.AddRange(SourceFCopen(cat.FCname));

                    var s = "";

                    foreach (var el in cat.S7Items)
                    {
                        if (!s.Equals(el.Codename))
                        {
                            s = el.Codename;
                            buffer.Add("NETWORK");
                            buffer.Add("TITLE = " + el.Codename);
                        }

                        buffer.AddRange(el.PeripheryCode.Select(p => p.Name).ToList());
                        buffer.Add("");
                        buffer.Add("");
                    }

                    buffer.AddRange(SourceFCclose());
                }
            }

            return buffer;
        }

        /// <summary>
        /// Вывод списка строк в текстовый файл
        /// </summary>
        /// <param name="filename">Имя файла (полное, с указанием пути)</param>
        /// <param name="list">Список строк</param>
        private void PrintListToFile(string filename, List<string> list)
        {
            var file = new StreamWriter(filename);

            file.WriteLine(Copyright);

            foreach (var line in list)
            {
                file.WriteLine(line);
            }

            file.Close();
        }


        /// <summary>
        /// Выгрузка списка блоков данных в строковый двумерный массив
        /// </summary>
        /// <returns></returns>
        public string[,] PrintDBlistToArray()
        {
            var arr = new string[0, 0];

            if (!IsEmptyDBlist())
            {
                var list = new List<string>();
                list = UniqDBlist[0].return_DBinRowForPrint();

                arr = new string[this.UniqDBlist.Count, list.Count];
                var i = 0;

                foreach (var el in UniqDBlist)
                {
                    list = el.return_DBinRowForPrint();
                    var j = 0;
                    foreach (var s in list)
                    {
                        arr[i, j] = s;
                        j++;
                    }
                    i++;
                }
            }
            return arr;
        }


        /// <summary>
        /// Создание списка элементов с неповторяющимся номеров блока данных
        /// </summary>
        /// <returns></returns>
        private bool GenerateUniqDbList()
        {
            var isGenerated = false;

            if (IsEmptyCategories()) return false;

            var sysNumList = new List<string>();
            var uniqSymbolsList = new List<SymbolTableItemModel>();

            foreach (var cat in this.Categories.OrderBy(k => k.Id))
            {
                foreach (var el in cat.S7Items.OrderByDescending(key => key.DbArrayIndex))
                {
                    if (!sysNumList.Contains(el.DbFullName))
                    {
                        sysNumList.Add(el.DbFullName);
                        uniqSymbolsList.Add(el);
                        isGenerated = true;
                    }
                }
                sysNumList.Clear();

                foreach (var el in uniqSymbolsList.OrderBy(k => k.SystemNumber))
                {
                    var db = new DataBlockModel();
                    // 11Y
                    if (el.DbArrayName.Equals("PID"))
                    {
                        db.SymbolName = "PID" + el.Codename;
                        db.Title = el.SignalComment;
                    }
                    else
                    {
                        db.SymbolName = el.SystemNumber + cat.Db.Symbol;
                        // Sensors 4-20
                        db.Title = cat.Description;
                    }

                    // DB111
                    db.FullName = el.DbFullName;

                    // SNS_UDT
                    db.UdtName = cat.Db.UdtName;

                    // Rounded size of DB Array[]
                    db.ArrayIndex = RoundToFive(el.DbArrayIndex);

                    // SNS
                    db.ArrayName = cat.Db.ArrayName;

                    UniqDBlist.Add(db);

                    isGenerated = true;
                }

                uniqSymbolsList.Clear();
            }
            return isGenerated;
        }

        /// <summary>
        /// Генерация текста исходного кода в синтаксисе STL 
        /// для последующего создания блоков данных в программе ПЛК
        /// </summary>
        /// <returns></returns>
        private List<string> GenerateStlDataBlocks()
        {
            var list = new List<string>();

            foreach (var db in UniqDBlist)
            {
                list.AddRange(db.ArrayName.Equals("PID") ? GenerateStlInstanceDb(db) : GenerateStlDb(db));
            }

            return list;
        }

        /// <summary>
        /// Генерация текста исходного кода в синтаксисе STL 
        /// для последующего создания Data Block в программе ПЛК
        /// </summary>
        /// <returns></returns>
        private List<String> GenerateStlDb(DataBlockModel db)
        {
            var list = new List<string>();

            list.Add("DATA_BLOCK " + db.FullName);
            list.Add("TITLE =" + db.Title);
            list.Add("AUTHOR:  Kratovi4");
            list.Add("VERSION : 2.0");
            list.Add("STRUCT");
            list.Add(db.ArrayName + ": ARRAY  [1 .. " + db.ArrayIndex + "] OF //Array");
            list.Add("\"" + db.UdtName + "\";");
            list.Add("END_STRUCT ;");
            list.Add("BEGIN");
            list.Add("END_DATA_BLOCK");
            list.Add("\r\n");

            return list;
        }


        /// <summary>
        /// Генерация текста исходного кода в синтаксисе STL 
        /// для последующего создания Instance DB на базе FB1 программе ПЛК
        /// </summary>
        /// <returns></returns>
        private List<String> GenerateStlInstanceDb(DataBlockModel db)
        {
            var list = new List<string>();

            list.Add("DATA_BLOCK " + db.FullName);
            list.Add("TITLE =" + db.Title);
            list.Add("{ S7_techparam := 'S7WRSAPX.Application' }");
            list.Add("AUTHOR:  Kratovi4");
            list.Add("FAMILY : STDCONT");
            list.Add("VERSION : 1.0");
            list.Add("FB 1");
            list.Add("BEGIN");
            list.Add("END_DATA_BLOCK");
            list.Add("\r\n");

            return list;
        }

        /// <summary>
        /// Генерация текста исходного кода в синтаксисе SCL 
        /// для создания функций-обработчиков логики в программе ПЛК
        /// </summary>
        /// <returns></returns>
        private List<String> GenerateSclFcLogic(string[] forWhat)
        {
            var list = new List<string>();
            var sorted = new List<DataBlockModel>(UniqDBlist.OrderBy(k => k.ArrayName).ThenBy(k => k.ArrayIndex));
            var countList = new List<int>();

            list.AddRange(ParseCodeFromFile(Rootdir + "\\" + TemplatesFolder + "\\openSCLcode.txt"));

            foreach (var db in sorted)
            {
                if (!countList.Contains(db.ArrayIndex))
                {
                    countList.Add(db.ArrayIndex);
                }
            }

            foreach (var uniq in countList.OrderBy(k => k))
            {

                list.Add("FOR i:=1 TO " + uniq.ToString() + " BY 1 DO");

                foreach (var db in sorted)
                {
                    if ((db.ArrayIndex == uniq) && (forWhat.Any(item => item == db.ArrayName)))
                        list.Add(GenerateSclLogicCode(db));
                }

                list.Add("END_FOR; ");
                list.Add("");
            }

            list.Add(CloseFc);

            return list;
        }


        private string GenerateSclLogicCode(DataBlockModel db)
        {
            var s = "";

            // we need something like this: Logic_SNS(SENSOR:="11A".SNS[i]); // 20
            s = "Logic_";
            s += db.ArrayName + "(" + db.ArrayName + ":=";
            s += "\"" + db.SymbolName + "\"";
            s += "." + db.ArrayName + "[i]";

            if (db.ArrayName.Equals("SNS") | db.ArrayName.Equals("SNC"))
            {
                s += ");  // " + db.ArrayIndex.ToString();
            }
            else
            {
                s += ",Clock:=clk); // " + db.ArrayIndex.ToString();
            }

            return s;
        }

        /// <summary>
        /// Генерация текста исходного кода
        /// для группового управления устройствами типа DRV
        /// </summary>
        /// <returns></returns>
        private List<string> GenerateStlGroupControl()
        {
            var buffer = new List<string>();
            var bufferModeA = new List<string>();
            var bufferModeM = new List<string>();
            var bufferAck = new List<string>();

            var bufferBlockOnOn = new List<string>();
            var bufferBlockOffOn = new List<string>();

            var bufferBlockOnOff = new List<string>();
            var bufferBlockOffOff = new List<string>();

            foreach (var cat in Categories)
            {
                if (cat.Db.ArrayName.ToString().Equals("DRV"))
                {
                    foreach (var drv in cat.S7Items.OrderBy(k => k.SystemNumber).ThenBy(k => k.DbArrayIndex))
                    {
                        if (drv.DeviceTag.Equals("Ctr"))
                        {
                            bufferModeA.Add("R " + drv.DbFullName + ".DRV[" + drv.DeviceNumber + "].Mode");
                            bufferModeM.Add("S " + drv.DbFullName + ".DRV[" + drv.DeviceNumber + "].Mode");
                            bufferAck.Add("S " + drv.DbFullName + ".DRV[" + drv.DeviceNumber + "].Ack");

                            bufferBlockOnOn.Add("S " + drv.DbFullName + ".DRV[" + drv.DeviceNumber + "].Block_on");
                            bufferBlockOnOff.Add("R " + drv.DbFullName + ".DRV[" + drv.DeviceNumber + "].Block_on");

                            bufferBlockOffOn.Add("S " + drv.DbFullName + ".DRV[" + drv.DeviceNumber + "].Block_off");
                            bufferBlockOffOff.Add("R " + drv.DbFullName + ".DRV[" + drv.DeviceNumber + "].Block_off");
                        }
                    }

                    var obj = cat.Db.ArrayName + cat.Db.Symbol;

                    buffer.Add("//--- Групповое управление ---");
                    buffer.Add("//--- " + cat.Description);
                    buffer.Add("//--- Все в автомат");
                    buffer.Add("A \"DB_OPTIONS\"." + obj + ".MODE_AUTO");
                    buffer.Add("FP \"DB_OPTIONS\"." + obj + ".MODE_AUTO_FP");
                    buffer.Add("JNB " + cat.Db.Symbol + "_MA");
                    buffer.Add("R \"DB_OPTIONS\"." + obj + ".MODE_AUTO");
                    buffer.Add(" ");
                    buffer.AddRange(bufferModeA);
                    buffer.Add(cat.Db.Symbol + "_MA" + ": NOP 0");
                    buffer.Add(" ");

                    buffer.Add("//--- Все в ручной");
                    buffer.Add("A \"DB_OPTIONS\"." + obj + ".MODE_MAN");
                    buffer.Add("FP \"DB_OPTIONS\"." + obj + ".MODE_MAN_FP");
                    buffer.Add("JNB " + cat.Db.Symbol + "_MM");
                    buffer.Add("R \"DB_OPTIONS\"." + obj + ".MODE_MAN");
                    buffer.Add(" ");
                    buffer.AddRange(bufferModeM);
                    buffer.Add(cat.Db.Symbol + "_MM" + ": NOP 0");
                    buffer.Add(" ");

                    buffer.Add("//--- Все квитировать");
                    buffer.Add("A \"DB_OPTIONS\"." + obj + ".ACK_ALL");
                    buffer.Add("FP \"DB_OPTIONS\"." + obj + ".ACK_ALL_FP");
                    buffer.Add("JNB " + cat.Db.Symbol + "ACK");
                    buffer.Add("R \"DB_OPTIONS\"." + obj + ".ACK_ALL");
                    buffer.Add(" ");
                    buffer.AddRange(bufferAck);
                    buffer.Add(cat.Db.Symbol + "ACK" + ": NOP 0");
                    buffer.Add(" ");

                    buffer.Add("//--- Блокировать I_on");
                    buffer.Add("A \"DB_OPTIONS\"." + obj + ".SET_BLOCK_ON_ALL");
                    buffer.Add("FP \"DB_OPTIONS\"." + obj + ".SET_BLOCK_ON_FP");
                    buffer.Add("JNB " + cat.Db.Symbol + "SBON");
                    buffer.Add("R \"DB_OPTIONS\"." + obj + ".SET_BLOCK_ON_ALL");
                    buffer.Add(" ");
                    buffer.AddRange(bufferBlockOnOn);
                    buffer.Add(cat.Db.Symbol + "SBON" + ": NOP 0");
                    buffer.Add(" ");

                    buffer.Add("//--- Разблокировать I_on");
                    buffer.Add("A \"DB_OPTIONS\"." + obj + ".RST_BLOCK_ON_ALL");
                    buffer.Add("FP \"DB_OPTIONS\"." + obj + ".RST_BLOCK_ON_FP");
                    buffer.Add("JNB " + cat.Db.Symbol + "RBON");
                    buffer.Add("R \"DB_OPTIONS\"." + obj + ".RST_BLOCK_ON_ALL");
                    buffer.Add(" ");
                    buffer.AddRange(bufferBlockOnOff);
                    buffer.Add(cat.Db.Symbol + "RBON" + ": NOP 0");
                    buffer.Add(" ");

                    buffer.Add("//--- Блокировать I_off");
                    buffer.Add("A \"DB_OPTIONS\"." + obj + ".SET_BLOCK_OFF_ALL");
                    buffer.Add("FP \"DB_OPTIONS\"." + obj + ".SET_BLOCK_OFF_FP");
                    buffer.Add("JNB " + cat.Db.Symbol + "SBOFF");
                    buffer.Add("R \"DB_OPTIONS\"." + obj + ".SET_BLOCK_OFF_ALL");
                    buffer.Add(" ");
                    buffer.AddRange(bufferBlockOffOn);
                    buffer.Add(cat.Db.Symbol + "SBOFF" + ": NOP 0");
                    buffer.Add(" ");

                    buffer.Add("//--- Разблокировать I_off");
                    buffer.Add("A \"DB_OPTIONS\"." + obj + ".RST_BLOCK_OFF_ALL");
                    buffer.Add("FP \"DB_OPTIONS\"." + obj + ".RST_BLOCK_OFF_FP");
                    buffer.Add("JNB " + cat.Db.Symbol + "RBOFF");
                    buffer.Add("R \"DB_OPTIONS\"." + obj + ".RST_BLOCK_OFF_ALL");
                    buffer.Add(" ");
                    buffer.AddRange(bufferBlockOffOff);
                    buffer.Add(cat.Db.Symbol + "RBOFF" + ": NOP 0");
                    buffer.Add(" ");
                }
            }

            bufferModeM = null;
            bufferModeA = null;
            bufferAck = null;

            return buffer;
        }

        /// <summary>
        /// Генерация текста исходного кода для блока обработки ПИД-регуляторов
        /// </summary>
        /// <returns></returns>
        private List<string> GenerateStlOb35Code()
        {
            var buffer = new List<string>();

            buffer.Add("//--- This code is in test mode!!");
            buffer.AddRange(ParseCodeFromFile(Rootdir + "\\" + TemplatesFolder + "\\openOB35code.txt"));

            foreach (var cat in Categories)
            {
                if (cat.Db.ArrayName.ToString().Equals("PID"))
                {
                    foreach (var pid in cat.S7Items.OrderBy(k => k.SystemNumber).ThenBy(k => k.DbArrayIndex))
                    {
                        buffer.Add("NETWORK");
                        buffer.Add("TITLE =" + pid.Codename);
                        buffer.Add("//--- Process value");
                        buffer.Add("//--- " + pid.SystemNumber + "A" + pid.DeviceNumber);
                        buffer.Add("L \"" + pid.SystemNumber + "A\".SNS[" + pid.DeviceNumber + "].value;");
                        buffer.Add("T " + pid.DbFullName + ".PV_IN;");

                        buffer.Add("CALL FB1, " + pid.DbFullName);
                        buffer.Add("( COM_RST:= #RESET,");
                        buffer.Add("CYCLE:= #CYCLE);");
                        buffer.Add("");

                        buffer.Add("//--- Periphery out value");
                        buffer.Add("L " + pid.DbFullName + ".LMN_PER;");
                        buffer.Add("T " + pid.SignalName + ";");
                        buffer.Add("\r\n\r\n");
                    }
                }
            }

            buffer.Add(CloseOb);
            return buffer;
        }

        /// <summary>
        /// Возвращает текст исходного кода на STL для функций 
        /// обработки логики импульсных счетчиков
        /// </summary>
        /// <param name="m">Стартовый адрес для меркерного пространства</param>
        /// <returns></returns>
        private List<string> GenerateStlLogicSnc(int m)
        {
            var buffer = new List<string>();

            buffer.Add("//--- SNC Logic auto generated code");
            buffer.Add("");

            var startLabel = 1001; // start label for Jump Commands

            foreach (var cat in Categories)
            {
                if (cat.Db.ArrayName.ToString().Equals("SNC"))
                {
                    foreach (var item in cat.S7Items.OrderBy(k => k.SystemNumber).ThenBy(k => k.DbArrayIndex))
                    {
                        var baseName = item.DbFullName + "." + item.DbArrayName + "[" + item.DbArrayIndex + "].";     // DB109.SNC[3].
                        var bit = 0;                                                                                        // bit number in merker, like M69.0

                        buffer.Add("");
                        buffer.Add("//--- Счетчик " + item.Codename);
                        buffer.Add("// Захват импульса");
                        buffer.Add("A " + item.SignalName);
                        buffer.Add("FP M" + m.ToString() + "." + bit.ToString());
                        buffer.Add("=" + baseName + item.DeviceTag);
                        buffer.Add("");
                        buffer.Add("// Инкремент счетчика на заданный шаг");
                        buffer.Add("A " + baseName + item.DeviceTag);
                        buffer.Add("A " + baseName + "start_count");
                        buffer.Add("JNB " + startLabel.ToString());
                        buffer.Add("L " + baseName + "summator");
                        buffer.Add("L " + baseName + "step");
                        buffer.Add("+D");
                        buffer.Add("T " + baseName + "summator");
                        buffer.Add(startLabel.ToString() + ": NOP 0");
                        buffer.Add("");
                        buffer.Add("// Сброс счетчика");
                        buffer.Add("A " + baseName + "reset_counter");
                        bit++;
                        buffer.Add("FP M" + m.ToString() + "." + bit.ToString());
                        buffer.Add("JCN " + startLabel++.ToString());
                        buffer.Add("L " + baseName + "summator");
                        buffer.Add("T " + baseName + "summator_prev");
                        buffer.Add("L 0");
                        buffer.Add("T " + baseName + "summator");
                        buffer.Add("R " + baseName + "reset_counter");
                        buffer.Add(startLabel.ToString() + ": NOP 0");
                        buffer.Add("");
                        //// Захват импульса
                        //      A     "I_17A02FC_COUNT"
                        //      FP    M     69.0
                        //      =     "DB_COUNTERS".FC17A02.pulse_signal

                        //// Инкремент счетчика на заданный шаг
                        //      A     "DB_COUNTERS".FC17A02.pulse_signal
                        //      A     "DB_COUNTERS".FC17A02.start_count
                        //      JNB   _111
                        //      L     "DB_COUNTERS".FC17A02.summator
                        //      L     "DB_COUNTERS".FC17A02.step
                        //      +D    
                        //      T     "DB_COUNTERS".FC17A02.summator
                        //_111: NOP   0

                        //// Сброс счетчика
                        //      A     "DB_COUNTERS".FC17A02.reset_counter
                        //      FP    M     69.2
                        //      JCN   _112

                        //      L     "DB_COUNTERS".FC17A02.summator
                        //      T     "DB_COUNTERS".FC17A02.summator_prev
                        //      L     0
                        //      T     "DB_COUNTERS".FC17A02.summator

                        //      R     "DB_COUNTERS".FC17A02.reset_counter

                        //_112: NOP   0

                        m++;
                    }
                }
            }
            return buffer;
        }

        /// <summary>
        /// Округление до 5-ти в большу сторону
        /// </summary>
        /// <param name="x">Число, которое необходимо округлить</param>
        /// <returns></returns>
        private static int RoundToFive(int x)
        {
            var r = 0;
            var b = 0;
            var max = 100;

            for (var j = 1; j < max; j++)
            {
                if ((b <= x) & (x <= b + 5))
                {
                    r = b + 5;
                    break;
                }

                b = b + 5;
            }
            return r;
        }

        /// <summary>
        /// Генерация текста исходного кода для заданного элемента списка сигналов
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private IEnumerable<string> GetPeripheryCode(SymbolTableItemModel item)
        {
            var p = new ObservableCollection<string>();

            var loadPath = item.DbFullName + "." + item.DbArrayName + "[" + item.DbArrayIndex + "]." + item.DeviceTag + ";";
            var loadSymbol = "\"" + item.SignalName + "\";";
            const string loadA = "A ";
            const string loadAn = "AN ";
            const string loadEq = "= ";
            const string loadL = "L ";
            const string loadT = "T ";

            var t = item.SignalType;


            switch (t)
            {
                case "Q":
                    p.Add(loadA + loadPath);
                    p.Add(loadEq + loadSymbol);
                    break;
                case "I":
                    if (item.DeviceType.Equals("Y"))
                    {
                        p.Add(loadAn + loadSymbol);
                    }
                    else
                        p.Add(loadA + loadSymbol);
                    p.Add(loadEq + loadPath);
                    break;
                case "IW":
                    p.Add(loadL + loadSymbol);
                    p.Add(loadT + loadPath);
                    break;
                case "QW":
                    //p.Add(load_L + load_path);
                    p.Add(loadL + item.DbFullName + ".LMN;");
                    p.Add(loadT + loadSymbol);
                    break;
                default:
                    p.Add("// There no automated generated code for " + item.Codename);
                    p.Add("NOP 0");
                    p.Add("// ");
                    break;
            }

            return p;
        }

        private static IEnumerable<string> SourceFCopen(string fCname)
        {

            var result = new List<string>();

            result.Add("");
            result.Add("//******* Function " + fCname + " ********");
            result.Add("FUNCTION \"" + fCname + "\" : VOID");
            result.Add("Title = " + fCname);
            result.Add("AUTHOR:  Kratovi4");
            result.Add("VERSION: 3.0");
            result.Add("BEGIN");
            result.Add("NETWORK");
            result.Add("");
            result.Add("");

            return result;
        }

        private static IEnumerable<string> SourceFCclose()
        {
            var result = new List<string>();
            result.Add("");
            result.Add("END_FUNCTION");
            return result;
        }

        private static IEnumerable<string> ParseCodeFromFile(string filename)
        {
            var result = new List<string>();

            try
            {
                var lines = File.ReadAllLines(filename);
                result.AddRange(lines);
            }
            catch (Exception e)
            {
                result.Add(e.Message);
            }

            return result;
        }

        public void MergePeripheryFiles()
        {
            var filenames = Directory
                .EnumerateFiles(Rootdir, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFullPath);
            var buffer = new List<string>();

            try
            {
                foreach (var f in filenames)
                {
                    if (f.Contains("periphery"))
                    {
                        buffer.AddRange(File.ReadAllLines(f));
                    }
                }

                buffer.Add("");

                File.WriteAllLines(Rootdir + "\\STL_ALL_PERIPHERY.txt", buffer);
            }
            catch (Exception e)
            {
                File.WriteAllText(Rootdir + "\\STL_ALL_PERIPHERY.txt", e.Message);
            }
        }
    }
}
