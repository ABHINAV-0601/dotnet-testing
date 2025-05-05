using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using CutGLib;
using System.Net.Mime;
using gsoApi.Models;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace gsoApi.Controllers
{
    [ApiController]
    [Produces(MediaTypeNames.Application.Json)]
    [Route("api/v1/[controller]")]
    public class GsoController : ControllerBase
    {
        private readonly ILogger<GsoController> _logger;
        public IConfiguration _config { get; }

        public GsoController(ILogger<GsoController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _config = configuration;
            //Note: All the calculation inputs & config values are in inches
            //i.e. 1/8 th of an inch is 0.125
        }

        [HttpPost]
        /*
        This Method demonstrates how to cut a 2D rectangular sheet/panels with different sizes.
        All parts cannot be rotated.
        */

        ///Todo: Need to implement HttpResponseMessage, Model validation
        //Todo: Need to implement basic Authentication 
        public GsoOutput GsoComputation(GsoInput gsoInput)
        {
            GsoOutput gsoOutput = new GsoOutput();
            List<string> logs = new List<string>();
            gsoOutput.log = logs;


            try
            {
                //Todo: this has to validate all the internal array paras too
                if (!ModelState.IsValid)
                {
                    logs.Add(string.Format("Error - {0}", ModelState));
                    Console.WriteLine("GSO OUTPUT: ");
                    Console.WriteLine(JsonSerializer.Serialize(gsoOutput));
                    return gsoOutput;
                }
                Console.WriteLine("GSO INPUT: ");
                Console.WriteLine(JsonSerializer.Serialize(gsoInput));
                #region input validation
                //Check if there exists some sheets in inventory
                if (gsoInput.stocks == null || gsoInput.stocks.Count == 0)
                {
                    logs.Add(string.Format("Error - No Sheets to cut"));
                    Console.WriteLine("GSO OUTPUT: ");
                    Console.WriteLine(JsonSerializer.Serialize(gsoOutput));
                    return gsoOutput;
                }

                //check if there exists order parts to be cut
                if (gsoInput.order_parts == null || gsoInput.order_parts.Count == 0)
                {
                    logs.Add(string.Format("Error - {0}", "No Order parts specified"));
                    return gsoOutput;
                }
                #endregion

                IConfiguration app_setting = _config.GetSection("App_Config");

                // First we create a new instance of the cut engine
                CutEngine Calculator = new CutEngine();

                //set the config values passed
                //if not available per request, fetch the default values from app-settings

                string key = "";

                if(Environment.GetEnvironmentVariable("SITE_LICENSE_KEY") != null)
                {
                    key = Environment.GetEnvironmentVariable("SITE_LICENSE_KEY");
                }

                #region set calculator config
                Calculator.SetSiteLicenseKey(
                  (gsoInput.site_license_key == default(string)) ?
                  key :
                  gsoInput.site_license_key
                );
                Calculator.UseLayoutMinimization
                  = (gsoInput.use_layout_minimization == default(bool)) ?
                  app_setting.GetValue<bool>("use_layout_minimization") :
                  gsoInput.use_layout_minimization;
                Calculator.MinimizeSheetRotation
                  = (gsoInput.minimize_sheet_rotation == default(bool)) ?
                    app_setting.GetValue<bool>("minimize_sheet_rotation") :
                    gsoInput.minimize_sheet_rotation;
                // Set the saw kerf value
                Calculator.SawWidth
                  = (gsoInput.saw_width == default(double)) ?
                    app_setting.GetValue<double>("saw_width") :
                    gsoInput.saw_width;
                // Set sheet trim sizes
                Calculator.TrimLeft
                  = (gsoInput.sheet_trim_left == default(double)) ?
                    app_setting.GetValue<double>("sheet_trim_left") :
                    gsoInput.sheet_trim_left;
                Calculator.TrimRight
                  = (gsoInput.sheet_trim_right == default(double)) ?
                    app_setting.GetValue<double>("sheet_trim_right") :
                    gsoInput.sheet_trim_right;
                Calculator.TrimTop
                  = (gsoInput.sheet_trim_top == default(double)) ?
                    app_setting.GetValue<double>("sheet_trim_top") :
                    gsoInput.sheet_trim_top;
                Calculator.TrimBottom
                  = (gsoInput.sheet_trim_bottom == default(double)) ?
                    app_setting.GetValue<double>("sheet_trim_bottom") :
                    gsoInput.sheet_trim_bottom;
                //decimal points, this can be defaulted to 4 decimals
                Calculator.MaxDecimalPoint
                  = (gsoInput.max_decimal_point == default(int)) ?
                    app_setting.GetValue<int>("max_decimal_point") :
                    gsoInput.max_decimal_point;
                //Defines how complex the result layout will be. It goes from 2 to 6
                Calculator.MaxCutLevel
                  = (gsoInput.max_cut_level == default(int)) ?
                    app_setting.GetValue<int>("max_cut_level") :
                    gsoInput.max_cut_level;

                //Kept below as param for future use
                Calculator.CompleteMode
                  = (gsoInput.complete_mode == default(bool)) ?
                    app_setting.GetValue<bool>("complete_mode") :
                    gsoInput.complete_mode;

                #endregion


                List<order_part_size> part_group = new List<order_part_size>();

                //Glass sheet counts will be added here
                foreach (stock_in st in gsoInput.stocks)
                {
                    Calculator.AddStock(st.width, st.height, st.count);
                }

                //For add parts we are defaulting; Count-1 & rotatable-true
                foreach (order_part ord_p in gsoInput.order_parts)
                {
                    if (ord_p.quantity > 1) {
                        for (int i = 1; i <= ord_p.quantity; i++) {
                            Calculator.AddPart(ord_p.width, ord_p.height, 1, true, ord_p.orderId + '-' + i);
                            //group the order parts by its light sizes.
                            group_parts_by_size(ord_p, part_group, i);
                        }
                    } else {
                        Calculator.AddPart(ord_p.width, ord_p.height, ord_p.quantity, true, ord_p.orderId);
                        //group the order parts by its light sizes.
                        group_parts_by_size(ord_p, part_group, 0);
                    }
                }


                // Run the calculation:
                string result = Calculator.Execute();
                //result == "" ; is success
                if (result == "")
                {
                    OutputSheetResults(Calculator, gsoOutput, part_group);

                    //Create a list of optimized orders.
                    List<String> optimized_orders = new List<string>();
                    if (gsoOutput.layouts != null)
                    {
                        optimized_orders = gsoOutput.layouts
                                          .Where(l => l.sheets != null)
                                          .SelectMany(l => l.sheets)
                                          .Where(s => s.order_parts != null)
                                          .SelectMany(s => s.order_parts)
                                          .Where(o => o.orderId != null)
                                          .Select(o => o.orderId).ToList();
                    }

                    List<string> missingOrderNos = new List<string>();
                    foreach (string item in optimized_orders) {
                        if (item.Contains('-')) {
                            string[] orderNumberArr = item.Split('-');
                            if (!missingOrderNos.Contains(orderNumberArr[0])) {
                                missingOrderNos.Add(orderNumberArr[0]);
                            }
                        }
                    }
                    if (missingOrderNos.Count > 0) {
                        foreach (string item in missingOrderNos) {
                            optimized_orders.Add(item);
                        }
                    }
                    //Create a list of un-optimized orders.
                    if (gsoOutput != null && gsoInput.order_parts != null)
                    {
                        gsoOutput.unoptimized_order_parts = gsoInput.order_parts
                                  .Where(o => !optimized_orders.Select(op => op).Contains(o.orderId)).ToList();
                    }else{
                        logs.Add(string.Format("Error - {0}", gsoOutput.unoptimized_order_parts));
                    }
                    
                }
                else
                {
                    logs.Add(string.Format("Error - {0}", result));
                }
            }
            catch (Exception ex)
            {
                logs.Add(string.Format("Error - {0}", ex));
            }

            Console.WriteLine("GSO OUTPUT: ");
            Console.WriteLine(JsonSerializer.Serialize(gsoOutput));
            return gsoOutput;
        }


        private void find_short_long_side(double l1, double l2, out double s_side, out double l_side)
        {
            if (l1 > l2)
            {
                s_side = l2;
                l_side = l1;
            }
            else
            {
                s_side = l1;
                l_side = l2;
            }
        }

        ///group the order parts by its light sizes 
        private void group_parts_by_size(order_part part, List<order_part_size> part_group, int i)
        {   
                   
            order_part_size partsize = new order_part_size();
            
            if (part_group != null && part_group.Count > 0)
            {
                double s_side, l_side;
                s_side = 0;
                l_side = 0;
                find_short_long_side(part.width, part.height, out s_side, out l_side);
                partsize.short_side = s_side;
                partsize.long_side = l_side;
                partsize.quantity = 1;
                partsize.times_processed = 0;
                //check if particular part size available
                int existing_group_number = part_group.Where(g => g.short_side == partsize.short_side && g.long_side == partsize.long_side)
                                .Select(g => g.group_number).FirstOrDefault<int>();

                if (existing_group_number == 0)
                {
                    //Create a new group
                    partsize.group_number = part_group.Max(g => g.group_number) + 1;
                }
                else
                {
                    //Part of existing group
                    partsize.group_number = existing_group_number;
                }

            }
            else
            {
                partsize.group_number = 1;
                double s_side, l_side;
                s_side = 0;
                l_side = 0;
                find_short_long_side(part.width, part.height, out s_side, out l_side);
                partsize.short_side = s_side;
                partsize.long_side = l_side;
                partsize.quantity = 1;

            }
            partsize.is_processed = false;
            partsize.orderId = i > 0 ? part.orderId + '-' + i : part.orderId;
            part_group.Add(partsize);


        }

        //Function used to mark a particular order as processed, once such order is placed on a layout.
        private void process_part_group(string orderId, List<order_part_size> part_group)
        {
             var query = (from part in part_group
                          where part.orderId == orderId
                          select part)
                               .Update(u => u.is_processed = true);
               // query.is_processed = true;
            //for (int i = 0; i < part_group.Count; i++)
            //{
                //if (part_group[i].orderId == orderId)
                //{
                   // part_group[i].times_processed += 1;
                    //if (part_group[i].times_processed == part_group[i].quantity)
                        //part_group[i].is_processed = true;
                //}
            //}
        }

        //Function used to find the next order-id which has similar size
        private string get_next_part_group(order_part_result op, List<order_part_size> part_group)
        {
            double s_side, l_side;
            find_short_long_side(op.width, op.height, out s_side, out l_side);

            s_side = Math.Round(s_side, 3);
            l_side = Math.Round(l_side, 3);

            return part_group.
                      Where(g => Math.Round(g.short_side, 3) == s_side && Math.Round(g.long_side, 3) == l_side && g.is_processed == false)
                      .Select(g => g.orderId).FirstOrDefault();
        }

        // This routine outputs the sheet results 
        private void OutputSheetResults(CutEngine aCalculator, GsoOutput gso_out, List<order_part_size> part_group)
        {

            List<layout> layouts = new List<layout>();
            gso_out.layouts = layouts;
            gso_out.CutGLib_version = aCalculator.Version;

            // Output linear layouts
            // Iterate by layouts
            for (int iLayout = 0; iLayout < aCalculator.LayoutCount; iLayout++)
            {
                int StockNo, StockCount;
                double SheetW, SheetH, partCount;
                bool active;

                layout _lay = new layout();

                List<stock_out> stock_Outs = new List<stock_out>();

                _lay.number = iLayout;
                _lay.sheets = stock_Outs;

                layouts.Add(_lay);
                // Get layout info
                aCalculator.GetLayoutInfo(_lay.number, out StockNo, out StockCount);
                
                for (int st = 1; st <= StockCount; st++)
                {
                    stock_out _st_out = new stock_out();
                    stock_Outs.Add(_st_out);
                    _st_out.number = StockNo;
                    _st_out.count = 1;

                    aCalculator.GetStockInfo(_st_out.number, out SheetW, out SheetH, out active);
                    _st_out.width = SheetW;
                    _st_out.height = SheetH;

                    // Output sheet parts on the stock
                    partCount = aCalculator.GetPartCountOnStock(_st_out.number);

                    List<order_part_result> parts = new List<order_part_result>();
                    _st_out.order_parts = parts;

                    // Iterate by the parts on the stock
                    for (int iPart = 0; iPart < partCount; iPart++)
                    {
                        int tmp;
                        double W = 0, H = 0, X = 0, Y = 0;
                        string orderId;
                        bool Rotated;

                        order_part_result part_out = new order_part_result();
                        parts.Add(part_out);

                        part_out.number = aCalculator.GetPartIndexOnStock(_st_out.number, iPart);

                        // Get sizes and location of the source part with index Iter 
                        aCalculator.GetResultPart(part_out.number, out tmp, out W, out H, out X, out Y, out Rotated, out orderId);
                        part_out.width = W;
                        part_out.height = H;
                        part_out.aX = X;
                        part_out.aY = Y;
                        part_out.rotated = Rotated;
                        //get the next order-id based on the group size
                        part_out.orderId = get_next_part_group(part_out, part_group);
                        //Mark the orderId as processed
                        process_part_group(part_out.orderId, part_group);
                    }

                    List<order_part_result> waste_parts = new List<order_part_result>();
                    _st_out.waste_parts = waste_parts;

                    // Output waste parts
                    partCount = aCalculator.GetRemainingPartCountOnStock(_st_out.number);
                    for (int iPart = 0; iPart < partCount; iPart++)
                    {
                        int tmp;
                        double W = 0, H = 0, X = 0, Y = 0;

                        order_part_result waster_part_out = new order_part_result();
                        waste_parts.Add(waster_part_out);

                        waster_part_out.number = aCalculator.GetRemainingPartIndexOnStock(_st_out.number, iPart);
                        // Get sizes and location of the source part with index Iter 
                        aCalculator.GetRemainingPart(waster_part_out.number, out tmp, out W, out H, out X, out Y);
                        waster_part_out.width = W;
                        waster_part_out.height = H;
                        waster_part_out.aX = X;
                        waster_part_out.aY = Y;

                    }
                    if (partCount == 0)
                    {
                        gso_out.log.Add("No Waste");
                    }

                    // gso_out.log = mylist;

                    // return mylist;

                }



            }
        }




    }

    public static class UpdateExtensions
    {
        public delegate void Func<TArg0>(TArg0 element);

        public static int Update<TSource>(this IEnumerable<TSource> source, Func<TSource> update)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (update == null) throw new ArgumentNullException("update");
            if (typeof(TSource).IsValueType)
                throw new NotSupportedException("value type elements are not supported by update.");

            int count = 0;
            foreach (TSource element in source)
            {
                update(element);
                count++;
            }
            return count;
        }
    }
}
