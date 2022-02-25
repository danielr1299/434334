using DataStuctures;
using Logic.InnerDataTypes;
using System;
using System.Threading;

namespace Logic
{
    public class Manager
    {
        private readonly DoubleLList<TimeData> timeQueueList;
        private readonly BST<DataX> mainTree;
        private readonly int maxPerBoxType;
        private readonly IUIComunicator comunicator;
        private readonly int maxDivides;
        private readonly Timer deleteOldBoxesTimer;

        public Manager
            (IUIComunicator comunicator, int maxPerBoxType = 50,
            int maxDivides = 3, int dueMinutes = 2, int periodMinutes = 3)
        {
            this.comunicator = comunicator;
            mainTree = new BST<DataX>();
            this.maxPerBoxType = maxPerBoxType;
            this.maxDivides = maxDivides;
            timeQueueList = new DoubleLList<TimeData>();
            TimeSpan due = new TimeSpan(0, 0, 0, 0);
            TimeSpan period = new TimeSpan(0, 0, 0, 0);
            deleteOldBoxesTimer = new Timer(DeleteOldBoxes, null, due, period);
        }

        private void DeleteOldBoxes(object state)
        {
            // Keep deleting while the list is not empty and the first is expired
            while (timeQueueList.Start != null && timeQueueList.Start.Data.Date < DateTime.Now)
            {
                // Remove the first node from list
                timeQueueList.RemoveFirst(out TimeData _);

                // Remove the x and y values from tree
                if (mainTree.Search(new DataX(timeQueueList.Start.Data.BoxX), out DataX x))
                {

                    // Remove the y value, no need to search it
                    x.YTree.Remove(new DataY(timeQueueList.Start.Data.BoxY));

                    // Remove the x value if it is empty
                    if (!x.YTree.HasRoot())
                    {
                        mainTree.Remove(x);
                    }
                }
            }
        }
        public void BoxDataNotPurchased()
        {
            DoubleLList<TimeData>.Node tmp = timeQueueList.Start;

            // Iterate over the list while the current tmp is expired
            while (tmp != null && tmp.Data.Date < DateTime.Now)
            {
                // find the box in the tree
                if (mainTree.Search(new DataX(tmp.Data.BoxX), out DataX x))
                {
                    if (x.YTree.Search(new DataY(tmp.Data.BoxY), out DataY y))
                    {
                        comunicator.OnMessage($"expires at: {tmp.Data.Date}, bottom size: {x.X}, height: {y.Y}, count: {y.Count}");
                        //Console.WriteLine(timeQueueList.ToString());
                    }
                }
                // Move to next element
                tmp = tmp.Next;
            }
        }
        public void MoveBoxToEnd(int bottomSize, int height)
        {
            DoubleLList<TimeData>.Node tmp = timeQueueList.Start;

            // Iterate over the list until the box is found or reached the end of the list
            while (tmp != null && !(tmp.Data.BoxX == bottomSize && tmp.Data.BoxY == height))
            {
                // Move to next element
                tmp = tmp.Next;
            }

            if (tmp == null)
            {
                // the box was not found in the list so just created a new one
                timeQueueList.AddLast(new TimeData(bottomSize, height));
            }
            else
            {
                // the box was found in the list move it to the end
                timeQueueList.MoveToEnd(tmp);
            }
        }
    


        public void Supply(double bottomSize, double height, int amount)
        {
            if (bottomSize > 30 || height > 30 || bottomSize <= 0 || height <= 0)
            {
                comunicator.OnError($"Invalid box with bottomSize: {bottomSize} and height: {height}");
                return;
            }

            //DataY y = new DataY(height);
            if (!mainTree.Search(new DataX(bottomSize), out DataX x))
            {
                timeQueueList.AddLast(new TimeData(bottomSize,height));
                //y.TimeListNodeRef = timeQueueList.End;
                x = new DataX(bottomSize);
                mainTree.Add(x);
            }

            if (!x.YTree.Search(new DataY(height), out DataY y))
            {
                timeQueueList.AddLast(new TimeData(bottomSize, height));
                //y.TimeListNodeRef = timeQueueList.End;
                y = new DataY(height);
                x.YTree.Add(y);
            }

            y.Count += amount;

            if (y.Count > maxPerBoxType)
            {
                comunicator.OnError
             ($"Box type with bottomSize: {bottomSize} and height: {height} has too many boxes ({y.Count}), returning {(y.Count - maxPerBoxType)} Boxes");
                y.Count = maxPerBoxType;
            }

            //timeQueueList.AddFirst(XXX);
            //DataY y = new DataY();
            //y.timeListNodeRef = timeQueueList.start;
        }

        public void BoxData(double bottomSize, double height)
        {
            if (!mainTree.Search(new DataX(bottomSize), out DataX x))
            {
                comunicator.OnMessage($"Box with bottomSize: {bottomSize} and height: {height} not found");
            }
            else
            {
                if (!x.YTree.Search(new DataY(height), out DataY y))
                {
                    comunicator.OnMessage($"Box with bottomSize: {bottomSize} and height: {height} not found");
                }
                else
                {
                    //y.TimeListNodeRef = new DoubleLList<TimeData>.Node(new TimeData(bottomSize, height));
                    comunicator.OnMessage("Data about Box:");
                    comunicator.OnMessage($"bottomSize: {x.X}");
                    comunicator.OnMessage($"height: {y.Y}");
                   // comunicator.OnMessage($"Date: {timeQueueList.Start.Data.Date}");
                    comunicator.OnMessage($"Count: {y.Count}");
                }
            }
        }

        public void Purchase(double bottomSize, double height, int count = 1)
        {
            double maxPercentage = 1.5;
            int divides = 0;

            while (divides < maxDivides && mainTree.HasRoot() && count > 0)
            {
                // Find best match in tree
                mainTree.SearchClosest(new DataX(bottomSize), out DataX x);

                if (x == null ||  x.X < bottomSize || x.X > bottomSize * maxPercentage)
                {
                    comunicator.OnError("not found any more boxes");
                    return;
                }

                    x.YTree.SearchClosest(new DataY(height), out DataY y);

                if (y == null || y.Y < height || y.Y > height * maxPercentage)
                {
                    comunicator.OnError("not found any more boxes");
                    return;
                }

                // Find y node in tree

                int boxesTaken = Math.Min(count, y.Count);

                if (!comunicator.OnQuestion($"Do you want { boxesTaken} boxes of bottomSize { x.X}, height = { y.Y}"))
                {
                    return;
                }
                else
                {
                    // Decrease count value
                    //y.TimeListNodeRef = timeQueueList.MoveToEnd(); לאחר כל קניה צריך להעביר את הקופסא לסוף
                    y.Count -= boxesTaken;
                    count -= boxesTaken;
                    divides++;

                    // Delete the y value if the count is 0
                    if (y.Count == 0)
                    {
                        x.YTree.Remove(y);
                        comunicator.OnMessage("It was the last box, so the box type was removed");

                        // Delete the x value if it has no nodes left
                        if (!x.YTree.HasRoot())
                        {
                            mainTree.Remove(x);
                        }
                    }
                }
            }
        }
        public void Menu()
        {
            int action = 0;


            while (action != 4)
            {
                Console.WriteLine("What do you want to do:");
                Console.WriteLine("1 - Supply");
                Console.WriteLine("2 - Show box data");
                Console.WriteLine("3 - Purchase");
                Console.WriteLine("4 - Exit program");
                action = Convert.ToInt32(Console.ReadLine());
                int height;
                int bottomSize;

                switch (action)
                {
                    case 1:
                        Console.WriteLine("Enter bottomSize:");
                        bottomSize = Convert.ToInt32(Console.ReadLine());

                        Console.WriteLine("Enter height:");
                        height = Convert.ToInt32(Console.ReadLine());

                        Console.WriteLine("Enter amount:");
                        int amount = Convert.ToInt32(Console.ReadLine());

                        Supply(bottomSize, height, amount);
                        break;
                    case 2:
                        Console.WriteLine("Enter bottomSize:");
                        bottomSize = Convert.ToInt32(Console.ReadLine());

                        Console.WriteLine("Enter height:");
                        height = Convert.ToInt32(Console.ReadLine());

                        BoxData(bottomSize, height);
                        break;
                    case 3:
                        Console.WriteLine("Enter bottomSize:");
                        bottomSize = Convert.ToInt32(Console.ReadLine());

                        Console.WriteLine("Enter height:");
                        height = Convert.ToInt32(Console.ReadLine());

                        Console.WriteLine("Enter count:");
                        int count = Convert.ToInt32(Console.ReadLine());

                        Purchase(bottomSize, height, count);
                        break;
                    case 4:
                        Console.WriteLine("Exit Program");
                        break;
                    default:
                        Console.WriteLine("Action not found");
                        break;
                }
            }
        
        }
    
    }
}
