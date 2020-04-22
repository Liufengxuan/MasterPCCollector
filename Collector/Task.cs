﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Collector.Channel;

namespace Collector
{

    public class Task<T> where T : ITaskContext
    {
        /// <summary>
        /// 创建一个工作任务单元
        /// </summary>
        /// <param name="channel"></param>
        public Task(BaseChannel channel)
        {
            _Chan = channel;
        }

        //************************************************************************************************************************************************************************

        #region 任务操作

        private List<T> TaskList = new List<T>();
        //  private List<T> FirstTaskList = new List<T>();
        private Queue<T> FirstTaskQueue = new Queue<T>();
        private Queue<T> AddTaskQueue = new Queue<T>();
        private Queue<T> RemoveTaskQueue = new Queue<T>();

        public int TaskCount
        {
            get
            {
                return TaskList.Count;
            }
        }
        public T GetTask(Predicate<T> match)
        {
            T t= TaskList.Find(match);
            if (t.TaskName!= null)
            {
                if (t.IsTempTask && t.IsSuccess)
                {
                    RemoveTaskToQueue(t);
                }
             
                return t;
            }
            return default(T);
            //for (int i = 0; i < TaskList.Count; i++)
            //{
            //    if (TaskList[i].TaskName == t.TaskName)
            //    {
            //        t = TaskList[i];
            //        if (t.IsTempTask && t.IsSuccess)
            //        {
            //            RemoveTaskToQueue(t);
            //        }
            //        return true;
            //    }
            //}
            //return false;
        }

        public List<T> GetAllTask(Predicate<T> match)
        {

            return TaskList.FindAll(match);
             
        }

        public void AddOrUpdateTaskToQueue(T t)
        {
            AddTaskQueue.Enqueue(t);
        }


        public void RemoveTaskToQueue(T t)
        {
            RemoveTaskQueue.Enqueue(t);
        }
        /// <summary>
        /// 添加一个任务、如果有TaskName相同的一条任务则会进行覆盖
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        private void AddOrUpdateTask(T t)
        {


            if (t.Priority == TaskPriority.High)
            {
                FirstTaskQueue.Enqueue(t);

                return;
            }

            for (int i = 0; i < TaskList.Count; i++)
            {
                if (TaskList[i].TaskName == t.TaskName)
                {
                    TaskList[i] = t;

                    return;
                }
            }
            TaskList.Add(t);

        }


        /// <summary>
        /// 根据TaskName删除元素
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private bool RemoveTask(T t)
        {
            lock (TaskList)
            {
                int rmIndex = -1;
                for (int i = 0; i < TaskList.Count; i++)
                {
                    if (TaskList[i].TaskName == t.TaskName)
                    {
                        rmIndex = i;
                        break;
                    }
                }
                if (rmIndex > -1)
                {
                    TaskList.RemoveAt(rmIndex);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 根据TaskName获取元素
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>

        //private bool GetFirstTask(ref T t)
        //{
        //    if (FirstTaskQueue.Count>0)
        //    {
        //        t = FirstTaskQueue.Dequeue();          
        //        return true;
        //    }
        //    return false;
        //}




        #region 循环迭代器
        private int CurrentTask = -1;

        /// <summary>
        /// -1~TaskList.Count-1 
        /// </summary>
        private int NextTask
        {
            get
            {
                CurrentTask++;
                if (CurrentTask > TaskList.Count - 1)
                {
                    if (CurrentTask == 0||TaskList.Count==0) CurrentTask = -1;//当任务数为零时
                    else CurrentTask = 0;//当任务数不为零但是超过了任务总数
                }
                return CurrentTask;
            }
        }
        private bool GetNextTask(ref T t)
        {
            int a = NextTask;
            if (a < 0) return false;
            t = TaskList[a];
            return true;
        }


        #endregion



        #endregion

        //************************************************************************************************************************************************************************
        /// <summary>
        /// 调用外部程序打开配置文件
        /// </summary>
        public void OpenConfig()
        {
            System.Diagnostics.Process.Start("notepad.exe", Parameters.ConfigPath);
        }




        //************************************************************************************************************************************************************************
        #region 通信管道相关操作
        /// <summary>
        /// 通信管道
        /// </summary>
        private BaseChannel _Chan;
        public BaseChannel CurrentChan
        {
            get { return _Chan; }
        }


        /// <summary>
        /// 注意、通信管道改变后 ，应该去注意通信报文是否需要做处理！！
        /// </summary>
        /// <param name="b"></param>
        public void ChangeChannel(BaseChannel b)
        {
            _Chan.Close();
            if (IsRun)
            {
                Stop();
                Thread.Sleep(20);
                _Chan = b;
                Run();
                return;
            }

            _Chan = b;



        }
        #endregion
        //************************************************************************************************************************************************************************
        /// <summary>
        /// 工作线程
        /// </summary>
        private Thread WorkThread;
        /// <summary>
        /// 连接设备失败至下次重试的间隔时间
        /// </summary>
        private int ReConnectWaitMillisecond = 200;
        //************************************************************************************************************************************************************************
        #region 异常消息 触发事件
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ex">异常信息</param>
        /// <param name="errCount">异常是第几次连续出现</param>
        public delegate void RecException(Exception ex, int errCount);

        /// <summary>
        /// 订阅此类抛出的异常消息
        /// </summary>
        public event RecException ExceptionEvent;
        #endregion
        //************************************************************************************************************************************************************************
        #region  任务执行状态 、停止操作、运行操作
        private bool _IsRun;
        public bool IsRun
        {

            get { return _IsRun; }
        }
        public void Run()
        {
            if (!_IsRun)
            {
                if (WorkThread != null)
                {
                    while (WorkThread.ThreadState == ThreadState.Running)
                    {
                        continue;
                    }
                }

                ReConnectWaitMillisecond = Convert.ToInt32(Parameters.iniOper.ReadIniData("Common", "ReConnectWaitMillisecond", ""));
                WorkThread = new Thread(Daemon);
                WorkThread.IsBackground = true;
                WorkThread.Priority = ThreadPriority.Highest;
                WorkThread.Start(null);
                _IsRun = true;
            }


        }
        public void Stop()
        {
            if (_IsRun)
            {
                if (_Chan != null)
                {
                    _Chan.Close();
                }
                _IsRun = false;
            }
        }



        #endregion

        //************************************************************************************************************************************************************************
        #region 工作方法   
        private int ErrCount = 0;
        private void Daemon(object p1)
        {
            while (true)
            {
                try
                {
                    if (!IsRun) return;
                    if (_Chan.GetState() == ChannelState.Closed)
                    {
                        _Chan.Open();
                    }

                    DoWork();
                }
                catch (Exception ex)
                {
                    ErrCount++;
                    ExceptionEvent?.Invoke(ex, ErrCount);
                    Thread.Sleep(ReConnectWaitMillisecond);
                }

            }
        }
        private void DoWork()
        {
            while (true)
            {

                if (!IsRun) return;
                if (_Chan.GetState() == ChannelState.Closed) return;

                if (AddTaskQueue.Count > 0)
                {
                    AddOrUpdateTask(AddTaskQueue.Dequeue());
                    continue;
                }
                if (RemoveTaskQueue.Count > 0)
                {
                    RemoveTask(RemoveTaskQueue.Dequeue());
                    continue;
                }






                T temp = default(T);
                if (FirstTaskQueue.Count > 0)
                {
                  
                    temp = FirstTaskQueue.Dequeue();
                    temp.Priority = TaskPriority.Normal;
                    temp.IsSuccess = false;
                    _Chan.Write(temp.GetTX());
                    temp.SetRX(_Chan.Read(256));
                    temp.IsSuccess = true;                   
                    AddOrUpdateTask(temp);
                }
                else if (GetNextTask(ref temp))
                {
                    if (temp.ExecuteOnce && temp.IsSuccess)
                    {
                        continue;
                    }
                    temp.IsSuccess = false;
                    _Chan.Write(temp.GetTX());
                    temp.SetRX(_Chan.Read(256));
                    temp.IsSuccess = true;
                    AddOrUpdateTask(temp);
                }
                else
                {
                    Thread.Sleep(20);
                }

                ErrCount = 0;

            }
        }

        #endregion











    }
}