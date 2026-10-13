using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace SysWeaver.Markdown
{
    /// <summary>
    /// Common GitHub emoji short codes (the names are also valid in Slack)
    /// </summary>
    static class MdEmoji
    {
        public static readonly FrozenDictionary<String, String> Codes = new Dictionary<String, String>(StringComparer.Ordinal)
        {
            //  Faces
            ["smile"] = "😄", ["smiley"] = "😃", ["grinning"] = "😀", ["grin"] = "😁", ["laughing"] = "😆", ["satisfied"] = "😆",
            ["joy"] = "😂", ["rofl"] = "🤣", ["sweat_smile"] = "😅", ["blush"] = "😊", ["innocent"] = "😇", ["wink"] = "😉",
            ["slightly_smiling_face"] = "🙂", ["upside_down_face"] = "🙃", ["relieved"] = "😌", ["heart_eyes"] = "😍", ["star_struck"] = "🤩",
            ["kissing_heart"] = "😘", ["yum"] = "😋", ["stuck_out_tongue"] = "😛", ["stuck_out_tongue_winking_eye"] = "😜", ["zany_face"] = "🤪",
            ["nerd_face"] = "🤓", ["sunglasses"] = "😎", ["partying_face"] = "🥳", ["smirk"] = "😏", ["unamused"] = "😒", ["disappointed"] = "😞",
            ["pensive"] = "😔", ["worried"] = "😟", ["confused"] = "😕", ["slightly_frowning_face"] = "🙁", ["persevere"] = "😣", ["tired_face"] = "😫",
            ["weary"] = "😩", ["pleading_face"] = "🥺", ["cry"] = "😢", ["sob"] = "😭", ["triumph"] = "😤", ["angry"] = "😠", ["rage"] = "😡",
            ["exploding_head"] = "🤯", ["flushed"] = "😳", ["hot_face"] = "🥵", ["cold_face"] = "🥶", ["scream"] = "😱", ["fearful"] = "😨",
            ["cold_sweat"] = "😰", ["sweat"] = "😓", ["hugs"] = "🤗", ["thinking"] = "🤔", ["shushing_face"] = "🤫", ["zipper_mouth_face"] = "🤐",
            ["neutral_face"] = "😐", ["expressionless"] = "😑", ["no_mouth"] = "😶", ["roll_eyes"] = "🙄", ["grimacing"] = "😬", ["lying_face"] = "🤥",
            ["sleeping"] = "😴", ["sleepy"] = "😪", ["drooling_face"] = "🤤", ["mask"] = "😷", ["face_with_thermometer"] = "🤒", ["nauseated_face"] = "🤢",
            ["sneezing_face"] = "🤧", ["dizzy_face"] = "😵", ["cowboy_hat_face"] = "🤠", ["money_mouth_face"] = "🤑", ["smiling_imp"] = "😈",
            ["skull"] = "💀", ["ghost"] = "👻", ["alien"] = "👽", ["robot"] = "🤖", ["poop"] = "💩", ["hankey"] = "💩", ["clown_face"] = "🤡",
            ["see_no_evil"] = "🙈", ["hear_no_evil"] = "🙉", ["speak_no_evil"] = "🙊",
            //  Hands and people
            ["+1"] = "👍", ["thumbsup"] = "👍", ["-1"] = "👎", ["thumbsdown"] = "👎", ["ok_hand"] = "👌", ["v"] = "✌️", ["crossed_fingers"] = "🤞",
            ["metal"] = "🤘", ["call_me_hand"] = "🤙", ["point_left"] = "👈", ["point_right"] = "👉", ["point_up"] = "☝️", ["point_up_2"] = "👆",
            ["point_down"] = "👇", ["raised_hand"] = "✋", ["hand"] = "✋", ["wave"] = "👋", ["clap"] = "👏", ["raised_hands"] = "🙌", ["open_hands"] = "👐",
            ["handshake"] = "🤝", ["pray"] = "🙏", ["writing_hand"] = "✍️", ["muscle"] = "💪", ["fist"] = "✊", ["facepunch"] = "👊", ["punch"] = "👊",
            ["eyes"] = "👀", ["eye"] = "👁️", ["brain"] = "🧠", ["baby"] = "👶", ["man"] = "👨", ["woman"] = "👩", ["person_shrugging"] = "🤷",
            ["shrug"] = "🤷", ["person_facepalming"] = "🤦", ["facepalm"] = "🤦", ["crown"] = "👑", ["tophat"] = "🎩", ["eyeglasses"] = "👓",
            //  Hearts and symbols
            ["heart"] = "❤️", ["orange_heart"] = "🧡", ["yellow_heart"] = "💛", ["green_heart"] = "💚", ["blue_heart"] = "💙", ["purple_heart"] = "💜",
            ["black_heart"] = "🖤", ["white_heart"] = "🤍", ["broken_heart"] = "💔", ["sparkling_heart"] = "💖", ["two_hearts"] = "💕", ["heartpulse"] = "💗",
            ["100"] = "💯", ["anger"] = "💢", ["boom"] = "💥", ["collision"] = "💥", ["dizzy"] = "💫", ["sweat_drops"] = "💦", ["dash"] = "💨",
            ["speech_balloon"] = "💬", ["thought_balloon"] = "💭", ["zzz"] = "💤", ["fire"] = "🔥", ["sparkles"] = "✨", ["star"] = "⭐", ["star2"] = "🌟",
            ["zap"] = "⚡", ["droplet"] = "💧", ["warning"] = "⚠️", ["no_entry"] = "⛔", ["no_entry_sign"] = "🚫", ["x"] = "❌",
            ["negative_squared_cross_mark"] = "❎", ["heavy_check_mark"] = "✔️", ["white_check_mark"] = "✅", ["ballot_box_with_check"] = "☑️",
            ["heavy_multiplication_x"] = "✖️", ["heavy_plus_sign"] = "➕", ["heavy_minus_sign"] = "➖", ["question"] = "❓", ["grey_question"] = "❔",
            ["exclamation"] = "❗", ["heavy_exclamation_mark"] = "❗", ["grey_exclamation"] = "❕", ["bangbang"] = "‼️", ["interrobang"] = "⁉️",
            ["information_source"] = "ℹ️", ["recycle"] = "♻️", ["copyright"] = "©️", ["registered"] = "®️", ["tm"] = "™️",
            ["red_circle"] = "🔴", ["orange_circle"] = "🟠", ["yellow_circle"] = "🟡", ["green_circle"] = "🟢", ["large_blue_circle"] = "🔵",
            ["purple_circle"] = "🟣", ["black_circle"] = "⚫", ["white_circle"] = "⚪", ["red_square"] = "🟥",
            ["green_square"] = "🟩", ["arrow_right"] = "➡️", ["arrow_left"] = "⬅️", ["arrow_up"] = "⬆️", ["arrow_down"] = "⬇️",
            ["arrows_counterclockwise"] = "🔄", ["ok"] = "🆗", ["new"] = "🆕", ["free"] = "🆓", ["cool"] = "🆒", ["sos"] = "🆘", ["up"] = "🆙",
            ["checkered_flag"] = "🏁", ["triangular_flag_on_post"] = "🚩", ["construction"] = "🚧", ["rotating_light"] = "🚨", ["beginner"] = "🔰",
            //  Objects
            ["tada"] = "🎉", ["confetti_ball"] = "🎊", ["balloon"] = "🎈", ["gift"] = "🎁", ["trophy"] = "🏆", ["medal_sports"] = "🏅",
            ["1st_place_medal"] = "🥇", ["2nd_place_medal"] = "🥈", ["3rd_place_medal"] = "🥉", ["dart"] = "🎯", ["video_game"] = "🎮", ["art"] = "🎨",
            ["rocket"] = "🚀", ["airplane"] = "✈️", ["car"] = "🚗", ["red_car"] = "🚗", ["bike"] = "🚲", ["ship"] = "🚢", ["bulb"] = "💡",
            ["flashlight"] = "🔦", ["memo"] = "📝", ["pencil"] = "📝", ["pencil2"] = "✏️", ["book"] = "📖", ["books"] = "📚", ["bookmark"] = "🔖",
            ["notebook"] = "📓", ["clipboard"] = "📋", ["calendar"] = "📆", ["date"] = "📅", ["hourglass"] = "⌛", ["hourglass_flowing_sand"] = "⏳",
            ["alarm_clock"] = "⏰", ["stopwatch"] = "⏱️", ["watch"] = "⌚", ["lock"] = "🔒", ["unlock"] = "🔓", ["key"] = "🔑", ["bell"] = "🔔",
            ["no_bell"] = "🔕", ["mag"] = "🔍", ["mag_right"] = "🔎", ["link"] = "🔗", ["paperclip"] = "📎", ["pushpin"] = "📌", ["round_pushpin"] = "📍",
            ["scissors"] = "✂️", ["email"] = "📧", ["envelope"] = "✉️", ["incoming_envelope"] = "📨", ["inbox_tray"] = "📥", ["outbox_tray"] = "📤",
            ["mailbox"] = "📫", ["package"] = "📦", ["phone"] = "☎️", ["telephone"] = "☎️", ["iphone"] = "📱", ["computer"] = "💻",
            ["desktop_computer"] = "🖥️", ["keyboard"] = "⌨️", ["printer"] = "🖨️", ["floppy_disk"] = "💾", ["cd"] = "💿", ["camera"] = "📷",
            ["movie_camera"] = "🎥", ["tv"] = "📺", ["radio"] = "📻", ["loudspeaker"] = "📢", ["mega"] = "📣", ["battery"] = "🔋",
            ["electric_plug"] = "🔌", ["gear"] = "⚙️", ["wrench"] = "🔧", ["hammer"] = "🔨", ["hammer_and_wrench"] = "🛠️", ["nut_and_bolt"] = "🔩",
            ["toolbox"] = "🧰", ["magnet"] = "🧲", ["test_tube"] = "🧪", ["microscope"] = "🔬", ["telescope"] = "🔭", ["shield"] = "🛡️",
            ["bug"] = "🐛", ["chart_with_upwards_trend"] = "📈", ["chart_with_downwards_trend"] = "📉", ["bar_chart"] = "📊",
            ["moneybag"] = "💰", ["dollar"] = "💵", ["euro"] = "💶", ["pound"] = "💷", ["yen"] = "💴", ["money_with_wings"] = "💸",
            ["credit_card"] = "💳", ["gem"] = "💎", ["file_folder"] = "📁", ["open_file_folder"] = "📂", ["page_facing_up"] = "📄",
            ["wastebasket"] = "🗑️", ["label"] = "🏷️", ["house"] = "🏠", ["office"] = "🏢", ["hospital"] = "🏥",
            ["school"] = "🏫", ["globe_with_meridians"] = "🌐", ["earth_africa"] = "🌍", ["earth_americas"] = "🌎", ["earth_asia"] = "🌏",
            ["world_map"] = "🗺️", ["compass"] = "🧭", ["anchor"] = "⚓", ["musical_note"] = "🎵", ["notes"] = "🎶",
            ["headphones"] = "🎧", ["microphone"] = "🎤", ["guitar"] = "🎸", ["soccer"] = "⚽", ["basketball"] = "🏀", ["football"] = "🏈",
            ["tennis"] = "🎾", ["golf"] = "⛳",
            //  Nature and food
            ["sunny"] = "☀️", ["partly_sunny"] = "⛅", ["cloud"] = "☁️", ["umbrella"] = "☔", ["snowflake"] = "❄️", ["snowman"] = "⛄",
            ["rainbow"] = "🌈", ["ocean"] = "🌊", ["crescent_moon"] = "🌙", ["seedling"] = "🌱", ["evergreen_tree"] = "🌲", ["deciduous_tree"] = "🌳",
            ["palm_tree"] = "🌴", ["cactus"] = "🌵", ["four_leaf_clover"] = "🍀", ["fallen_leaf"] = "🍂", ["rose"] = "🌹", ["sunflower"] = "🌻",
            ["tulip"] = "🌷", ["cherry_blossom"] = "🌸", ["bouquet"] = "💐", ["dog"] = "🐶", ["cat"] = "🐱", ["mouse"] = "🐭", ["rabbit"] = "🐰",
            ["fox_face"] = "🦊", ["bear"] = "🐻", ["panda_face"] = "🐼", ["koala"] = "🐨", ["tiger"] = "🐯", ["lion"] = "🦁", ["cow"] = "🐮",
            ["pig"] = "🐷", ["frog"] = "🐸", ["monkey_face"] = "🐵", ["chicken"] = "🐔", ["penguin"] = "🐧", ["bird"] = "🐦", ["owl"] = "🦉",
            ["unicorn"] = "🦄", ["bee"] = "🐝", ["honeybee"] = "🐝", ["butterfly"] = "🦋", ["snail"] = "🐌", ["turtle"] = "🐢", ["snake"] = "🐍",
            ["octopus"] = "🐙", ["fish"] = "🐟", ["whale"] = "🐳", ["dolphin"] = "🐬", ["crab"] = "🦀", ["apple"] = "🍎", ["green_apple"] = "🍏",
            ["banana"] = "🍌", ["grapes"] = "🍇", ["strawberry"] = "🍓", ["watermelon"] = "🍉", ["lemon"] = "🍋", ["peach"] = "🍑", ["cherries"] = "🍒",
            ["avocado"] = "🥑", ["tomato"] = "🍅", ["carrot"] = "🥕", ["corn"] = "🌽", ["hot_pepper"] = "🌶️", ["bread"] = "🍞", ["cheese"] = "🧀",
            ["egg"] = "🥚", ["hamburger"] = "🍔", ["fries"] = "🍟", ["pizza"] = "🍕", ["hotdog"] = "🌭", ["taco"] = "🌮", ["burrito"] = "🌯",
            ["sushi"] = "🍣", ["ramen"] = "🍜", ["spaghetti"] = "🍝", ["cake"] = "🍰", ["birthday"] = "🎂", ["cookie"] = "🍪", ["doughnut"] = "🍩",
            ["chocolate_bar"] = "🍫", ["candy"] = "🍬", ["lollipop"] = "🍭", ["icecream"] = "🍦", ["popcorn"] = "🍿", ["coffee"] = "☕", ["tea"] = "🍵",
            ["beer"] = "🍺", ["beers"] = "🍻", ["wine_glass"] = "🍷", ["cocktail"] = "🍸", ["champagne"] = "🍾", ["clinking_glasses"] = "🥂",
        }.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
