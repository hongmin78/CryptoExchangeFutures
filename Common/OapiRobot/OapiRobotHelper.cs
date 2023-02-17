﻿using LinqKit;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.OapiRobot
{
    public class OapiRobotHelper
    {
        public static string dd_host = "https://oapi.dingtalk.com/";
        public static string RobotSendUrl = "robot/send?access_token=1cb03f6f04e3aea0dd517bc46069aec5f8bfb7988eda9b6e32d2225539b0091a";

        /// <summary>
        /// 发送钉钉提醒
        /// </summary>
        /// <param name="content">发送内容</param>
        /// <returns></returns>
        public static async Task Message(string content, string key = "-_-")
        {
            WebhookModel model = new WebhookModel();
            model.msgtype = "text";
            model.text = new WebhookModelText();
            model.text.content = $"{content}";
            await Message(model, key, $"{dd_host}{RobotSendUrl}");
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="model">钉钉型号</param>
        /// <param name="key">关键字</param>
        /// <param name="token">令牌地址</param>
        /// <returns></returns>
        private static async Task Message(WebhookModel model, string key, string token)
        {
            model.text.content = $"{key}:{model.text.content}";
            string content = JsonConvert.SerializeObject(model);
            var buffer = Encoding.UTF8.GetBytes(content);
            var byteContent = new ByteArrayContent(buffer);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            try
            {
                HttpClient client = new HttpClient();
                var response = await client.PostAsync(token, byteContent);
                string result = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                { 
                   Console.WriteLine($"钉钉机器人发送群消息出错:群内容为:{content}");
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"钉钉机器人发送群消息出错:群内容为:{content}");
            }
        }

        /// <summary>
        /// 发起请求
        /// </summary>
        /// <param name="url">地址</param>
        /// <param name="data">数据</param>
        /// <param name="reqtype">请求类型</param>
        /// <returns></returns>
        public static string Request(string url, string data, string reqtype)
        {
            HttpWebRequest web = (HttpWebRequest)WebRequest.Create(dd_host + url);
            web.ContentType = "application/json";
            web.Method = reqtype;
            if (data.Length > 0 && reqtype.Trim().ToUpper() == "POST")
            {
                byte[] postBytes = Encoding.UTF8.GetBytes(data);
                web.ContentLength = postBytes.Length;
                using (Stream reqStream = web.GetRequestStream())
                {
                    reqStream.Write(postBytes, 0, postBytes.Length);
                }
            }
            string html = string.Empty;
            using (HttpWebResponse response = (HttpWebResponse)web.GetResponse())
            {
                Stream responseStream = response.GetResponseStream();
                StreamReader streamReader = new StreamReader(responseStream, Encoding.UTF8);
                html = streamReader.ReadToEnd();
            }
            return html;
        }
        /// <summary>
        /// 发送文本消息
        /// </summary>
        /// <param name="content">文本内容</param>
        /// <param name="atMobiles">@人员电话</param>
        /// <param name="isAtAll">是否@群所有成员</param>
        public static void OapiRobotText(string content, List<string> atMobiles, bool isAtAll)
        {
            TextModel tModel = new TextModel();
            tModel.at = new atText();
            tModel.text = new text();
            tModel.at.atMobiles = new List<string>();

            tModel.text.content = content;
            tModel.at.atMobiles.AddRange(atMobiles);
            tModel.at.isAtAll = isAtAll;
            tModel.msgtype = "text";

            string data = JsonConvert.SerializeObject(tModel);
            string json = Request(RobotSendUrl, data, "POST");
        }
        /// <summary>
        /// 发送Link消息
        /// </summary>
        /// <param name="title">消息标题</param>
        /// <param name="text">消息内容。如果太长只会部分展示</param>
        /// <param name="messageUrl">点击消息跳转的URL</param>
        /// <param name="picUrl">图片URL</param>
        public static void OapiRobotLink(string title, string text, string messageUrl, string picUrl)
        {
            LinkModel lModel = new LinkModel();
            lModel.link = new link();
            lModel.msgtype = "link";
            lModel.link.title = title;
            lModel.link.text = text;
            lModel.link.messageUrl = messageUrl;
            lModel.link.picUrl = picUrl;

            string data = JsonConvert.SerializeObject(lModel);
            string json = Request(RobotSendUrl, data, "POST");
        }
        /// <summary>
        /// 发送markdown类消息
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="text">消息主体</param>
        /// <param name="atMobiles">@人员电话</param>
        /// <param name="isAtAll">是否@群所有成员</param>
        public static void OapiRobotMarkDown(string title, string text, List<string> atMobiles, bool isAtAll)
        {
            MarkDownModel mdModel = new MarkDownModel();
            mdModel.at = new atMarkdown();
            mdModel.markdown = new markdown();
            mdModel.at.atMobiles = new List<string>();

            mdModel.markdown.title = title;
            mdModel.markdown.text = text;
            mdModel.at.atMobiles.AddRange(atMobiles);
            mdModel.at.isAtAll = isAtAll;
            mdModel.msgtype = "markdown";

            string data = JsonConvert.SerializeObject(mdModel);
            string json = Request(RobotSendUrl, data, "POST");
        }

        /// <summary>
        /// 发送整体跳转ActionCard类型
        /// </summary>
        /// <param name="title">首屏会话透出的展示内容</param>
        /// <param name="text">markdown格式的消息</param>
        /// <param name="singleTitle">单个按钮的方案。(设置此项和singleURL后btns无效。)</param>
        /// <param name="singleURL">点击singleTitle按钮触发的URL</param>
        /// <param name="btnOrientation">0-按钮竖直排列，1-按钮横向排列</param>
        /// <param name="hideAvatar">0-正常发消息者头像,1-隐藏发消息者头像</param>
        public static void OapiRobotActionCardOverall(string title, string text, string singleTitle, string singleURL, string btnOrientation, string hideAvatar)
        {
            ActionCardOverallModel acModel = new ActionCardOverallModel();
            acModel.actionCard = new actionCard();

            acModel.msgtype = "actionCard";
            acModel.actionCard.title = title;
            acModel.actionCard.text = text;
            acModel.actionCard.singleTitle = singleTitle;
            acModel.actionCard.singleURL = singleURL;
            if (!string.IsNullOrEmpty(btnOrientation))
                acModel.actionCard.btnOrientation = btnOrientation;
            else
                acModel.actionCard.btnOrientation = "0";
            if (!string.IsNullOrEmpty(hideAvatar))
                acModel.actionCard.hideAvatar = hideAvatar;
            else
                acModel.actionCard.hideAvatar = "0";

            string data = JsonConvert.SerializeObject(acModel);
            string json = Request(RobotSendUrl, data, "POST");
        }

        /// <summary>
        /// 发送独立跳转ActionCard类型
        /// </summary>
        /// <param name="title"></param>
        /// <param name="text"></param>
        /// <param name="btns"></param>
        /// <param name="btnOrientation"></param>
        /// <param name="hideAvatar"></param>
        public static void OapiRobotActionCardSingle(string title, string text, string btns, string btnOrientation, string hideAvatar)
        {
            ActionCardSingleModel acModel = new ActionCardSingleModel();
            acModel.actionCard = new actionCardSingle();
            acModel.actionCard.btns = JsonConvert.DeserializeObject<List<btns>>(btns);//这里使用字符串拼接的json格式转化为List<model>,下面一个方法使用List<model>中直接添加值
            acModel.msgtype = "actionCard";
            acModel.actionCard.title = title;
            acModel.actionCard.text = text;
            if (!string.IsNullOrEmpty(btnOrientation))
                acModel.actionCard.btnOrientation = btnOrientation;
            else
                acModel.actionCard.btnOrientation = "0";
            if (!string.IsNullOrEmpty(hideAvatar))
                acModel.actionCard.hideAvatar = hideAvatar;
            else
                acModel.actionCard.hideAvatar = "0";
            string data = JsonConvert.SerializeObject(acModel);
            string json = Request(RobotSendUrl, data, "POST");
        }
        /// <summary>
        /// 发送FeedCard类型
        /// </summary>
        public static void OapiRobotFeedCard()
        {
            FeedCardModel fcModel = new FeedCardModel();
            fcModel.msgtype = "feedCard";
            fcModel.feedCard = new feedCard();
            fcModel.feedCard.links = new List<links>();//使用List<model>中直接添加值

            links model1 = new links();
            model1.title = "[消息] 时代的火车向前开";
            model1.messageURL = "https://mp.weixin.qq.com/s?__biz=MzA4NjMwMTA2Ng==&mid=2650316842&idx=1&sn=60da3ea2b29f1dcc43a7c8e4a7c97a16&scene=2&srcid=09189AnRJEdIiWVaKltFzNTw&from=timeline&isappinstalled=0&key=&ascene=2&uin=&devicetype=android-23&version=26031933&nettype=WIFI";
            model1.picURL = "https://www.dingtalk.com/";
            fcModel.feedCard.links.Add(model1);

            links model2 = new links();
            model2.title = "[消息] 时代的火车向前开2";
            model2.messageURL = "https://mp.weixin.qq.com/s?__biz=MzA4NjMwMTA2Ng==&mid=2650316842&idx=1&sn=60da3ea2b29f1dcc43a7c8e4a7c97a16&scene=2&srcid=09189AnRJEdIiWVaKltFzNTw&from=timeline&isappinstalled=0&key=&ascene=2&uin=&devicetype=android-23&version=26031933&nettype=WIFI";
            model2.picURL = "https://www.dingtalk.com/";
            fcModel.feedCard.links.Add(model2);

            string data = JsonConvert.SerializeObject(fcModel);
            string json = Request(RobotSendUrl, data, "POST");
        }
    }

    /// <summary>
    /// 钉钉webhook模型
    /// </summary>
    public class WebhookModel
    {
        /// <summary>
        /// 
        /// </summary>
        public string msgtype { get; set; } = null!;
        /// <summary>
        /// 
        /// </summary>
        public WebhookModelText text { get; set; } = null!;
    }

    /// <summary>
    /// 钉钉webhook模型
    /// </summary>
    public class WebhookModelText
    {
        /// <summary>
        /// IDCM主流币机器人
        /// </summary>
        public string content { get; set; } = null!;
    }
}
