using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreationModelPlugin
{
    #region ЗАНЯТИЕ 6. ПЛАГИН "СОЗДАНИЕ МОДЕЛИ". ЧАСТЬ 3.
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModelPlugin : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            #region Пример фильтрации стен
            var res1 = new FilteredElementCollector(doc) //часть методов, это RevitApi, а часть Linq
                .OfClass(typeof(Wall)) //typeof - объектно ориентированное представление типа (карточка, информация о типе). Фильтры ревита быстрее сделают фильтрацию, и уже после можно Linq
                                       //.Cast<Wall>() //преобразование каждого элемента в списке в тип
                .OfType<Wall>() //метод, выполняющий фильтрацию на основе заданного типа. Этот метод будет работать даже без .OfClass(typeof(Wall)), но медленнее
                .ToList();
            //ищем типы стен (WallType)
            var res2 = new FilteredElementCollector(doc) //часть методов, это RevitApi, а часть Linq
                .OfClass(typeof(WallType)) //typeof - объектно ориентированное представление типа (карточка, информация о типе). Фильтры ревита быстрее сделают фильтрацию, и уже после можно Linq
                                           //.Cast<Wall>() //преобразование каждого элемента в списке в тип
                .OfType<WallType>() //метод, выполняющий фильтрацию на основе заданного типа. Этот метод будет работать даже без .OfClass(typeof(Wall)), но медленнее
                .ToList();
            //загружаемые семейства 
            var res3 = new FilteredElementCollector(doc) //часть методов, это RevitApi, а часть Linq
                .OfClass(typeof(FamilyInstance)) //typeof - объектно ориентированное представление типа (карточка, информация о типе). Фильтры ревита быстрее сделают фильтрацию, и уже после можно Linq
                .OfCategory(BuiltInCategory.OST_Doors) //метод для загружаемых семейств
                                                       //.Cast<Wall>() //преобразование каждого элемента в списке в тип
                .OfType<FamilyInstance>() //метод, выполняющий фильтрацию на основе заданного типа. Этот метод будет работать даже без .OfClass(typeof(Wall)), но медленнее
                .ToList();
            //для конкретного типа загружаемого семейства
            //загружаемые семейства 
            var res4 = new FilteredElementCollector(doc) //часть методов, это RevitApi, а часть Linq
                .OfClass(typeof(FamilyInstance)) //typeof - объектно ориентированное представление типа (карточка, информация о типе). Фильтры ревита быстрее сделают фильтрацию, и уже после можно Linq
                .OfCategory(BuiltInCategory.OST_Doors) //метод для загружаемых семейств. Медленный Revit фильтр
                                                       //.Cast<Wall>() //преобразование каждого элемента в списке в тип
                .OfType<FamilyInstance>() //метод, выполняющий фильтрацию на основе заданного типа. Этот метод будет работать даже без .OfClass(typeof(Wall)), но медленнее
                .Where(x => x.Name.Equals("0915 x 2314 мм"))
                .ToList();
            #endregion
            /* это может быть долго, поэтому переработаем код
            Level level1=new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Where(x => x.Name.Equals("Level 1"))
                .OfType<Level>()
                .FirstOrDefault(); //первый элемент
            */

            List<Level> listLevel = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();
            Level level1 = listLevel
                .Where(x => x.Name.Equals("Уровень 1"))
                .FirstOrDefault();
            Level level2 = listLevel
                .Where(x => x.Name.Equals("Уровень 2"))
                .FirstOrDefault();
            //формируем список с координатами
            double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters); //ширина. Конвертируем в мм
            double dept = UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters); //глубина. Конвертируем в мм
            double dx = width / 2;
            double dy = dept / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0)); //благодаря этому можно зациклить список и попарно построить линии из точек

            List<Wall> walls = new List<Wall>(); //список созданных стен

            Transaction transaction = new Transaction(doc, "Построение стен");
            transaction.Start();
            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, level1.Id, false);
                walls.Add(wall);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);
                
                //AddDoor(doc, level1, walls[0]);
                AddRoof(doc, level2, walls);
                //AddRoof2(doc, level2, walls);
            }
            transaction.Commit();
            return Result.Succeeded;
        }

        private void AddRoof2(Document doc, Level level2, List<Wall> walls)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 125мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();
            //Добавим смещение крыши относительно оси стены
            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;
            List<XYZ> points = new List<XYZ>(); //собираем в список
            points.Add(new XYZ(-dt, -dt, 0));
            points.Add(new XYZ(dt, -dt, 0));
            points.Add(new XYZ(dt, dt, 0));
            points.Add(new XYZ(-dt, dt, 0));
            points.Add(new XYZ(-dt, -dt, 0));

            Application application = doc.Application;
            CurveArray curveArray = application.Create.NewCurveArray(); //создаём кривую
            for (int i = 0; i < 4; i++)
            {
                LocationCurve curve = walls[i].Location as LocationCurve; //получаем кривые
                //footprint.Append(curve.Curve); //добавляем с помощью метода Append..Curve получает кривую из LocationCurve. Метод строит до середины стены
                XYZ p1 = curve.Curve.GetEndPoint(0);
                XYZ p2 = curve.Curve.GetEndPoint(1);
                Line line = Line.CreateBound(p1 + points[i], p2 + points[i + 1]);
                curveArray.Append(line);
            }
            ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), doc.ActiveView);
            doc.Create.NewExtrusionRoof(curveArray, plane, level2, roofType, 0, 40);
        }

        private void AddRoof(Document doc, Level level2, List<Wall> walls) //создаём крышу
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 125мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();

            //Добавим смещение крыши относительно оси стены
            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;
            List<XYZ> points = new List<XYZ>(); //собираем в список
            points.Add(new XYZ(-dt, -dt, 0));
            points.Add(new XYZ(dt, -dt, 0));
            points.Add(new XYZ(dt, dt, 0));
            points.Add(new XYZ(-dt, dt, 0));
            points.Add(new XYZ(-dt, -dt, 0));

            Application application = doc.Application;
            CurveArray footprint = application.Create.NewCurveArray(); //создаём кривую
            for (int i = 0; i < 4; i++)
            {
                LocationCurve curve = walls[i].Location as LocationCurve; //получаем кривые
                //footprint.Append(curve.Curve); //добавляем с помощью метода Append..Curve получает кривую из LocationCurve. Метод строит до середины стены
                XYZ p1 = curve.Curve.GetEndPoint(0);
                XYZ p2 = curve.Curve.GetEndPoint(1);
                Line line = Line.CreateBound(p1 + points[i], p2 + points[i + 1]);
                footprint.Append(line);
            }
            ModelCurveArray footPrintToModelCurveMapping = new ModelCurveArray(); //экземпляр класса
            FootPrintRoof footprintRoof = doc.Create.NewFootPrintRoof(footprint, level2, roofType, out footPrintToModelCurveMapping);
            #region Уклон крыши. Вариант 1
            //ModelCurveArrayIterator iterator = footPrintToModelCurveMapping.ForwardIterator();
            //iterator.Reset();
            //while (iterator.MoveNext())
            //{
            //    ModelCurve modelCurve = iterator.Current as ModelCurve;
            //    footprintRoof.set_DefinesSlope(modelCurve, true);
            //    footprintRoof.set_SlopeAngle(modelCurve, 0.5);
            //}
            #endregion
            #region Уклон крыши. Вариант 2
            //foreach(ModelCurve m in footPrintToModelCurveMapping)
            //{
            //    footprintRoof.set_DefinesSlope(m, true);
            //    footprintRoof.set_SlopeAngle(m, 0.5);
            //}
            #endregion
        }
        private void AddDoor(Document doc, Level level1, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("750 x 2000 мм"))
                .Where(x => x.FamilyName.Equals("Дверь-Одинарная-Щитовая_Панель_Двусторонняя"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            if (!doorType.IsActive)
                doorType.Activate();

            doc.Create.NewFamilyInstance(point, doorType, wall, level1, StructuralType.NonStructural);

        }
    }
    #endregion
}
