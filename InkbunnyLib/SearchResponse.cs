﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InkbunnyLib {
    public class SearchResponse : InkbunnyResponse {
        public class SearchParam {
            public int keyword_id;
            public string keyword_name;
            public int submissions_count;
        }

        public class Keyword {
            public string param_name;
            public string param_value;
        }

        public string sid;
        public string user_location;
        public int results_count_all;
        public int results_count_thispage;
        public int pages_count;
        public int page;
        public string rid;
        public string rid_ttl;
        public SearchParam[] search_params;
        public Keyword[] keyword_list;
        public InkbunnySubmission[] submissions;
    }
}
