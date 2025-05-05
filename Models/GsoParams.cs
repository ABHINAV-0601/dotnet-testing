using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace gsoApi.Models
{
    public class GsoInput
    {

        // Below are config values
        public string site_license_key { set; get; }
        public double saw_width { get; set; }
        public bool use_layout_minimization { get; set; }
        public bool minimize_sheet_rotation { get; set; }
        public double sheet_trim_left { get; set; }
        public double sheet_trim_right { get; set; }
        public double sheet_trim_top { get; set; }
        public double sheet_trim_bottom { get; set; }

        //this is just kept future capabilities
        public bool complete_mode { get; set; }
        public int max_decimal_point { get; set; }
        public int max_cut_level { get; set; }
        public IList<stock_in> stocks { get; set; }
        public IList<order_part> order_parts { get; set; }
    }

    public class GsoOutput
    {
        public string CutGLib_version { get; set; }

        public IList<layout> layouts { get; set; }
        public IList<string> log { get; set; }
        public IList<order_part> unoptimized_order_parts { get; set; }
    }

    public class layout
    {
        public int number { get; set; }
        public IList<stock_out> sheets { get; set; }
    }

    public class stock_out
    {
        public int number { get; set; }
        public double width { get; set; }
        public double height { get; set; }
        public int count { get; set; }
        public IList<order_part_result> order_parts { get; set; }
        public IList<order_part_result> waste_parts { get; set; }
    }

    public class stock_in
    {
        public double width { get; set; }
        public double height { get; set; }
        public int count { get; set; }
    }

    public class order_part
    {
        public double width { get; set; }
        public double height { get; set; }
        public string orderId { get; set; }
        public int quantity { get; set; }

    }

    public class order_part_size
    {
        public double short_side { get; set; }
        public double long_side { get; set; }
        public string orderId { get; set; }
        public int quantity { get; set; }
        public int times_processed { get; set; }
        public bool is_processed { get; set; }
        public int group_number { get; set; }

    }

    public class order_part_result
    {
        public int number { get; set; }
        public int stock { get; set; }
        public double width { get; set; }
        public double height { get; set; }
        public string orderId { get; set; }
        public double aX { get; set; }
        public double aY { get; set; }
        public bool rotated { get; set; }

    }
}
