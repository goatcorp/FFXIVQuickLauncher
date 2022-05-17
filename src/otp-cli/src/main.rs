use std::time::{SystemTime, UNIX_EPOCH};
use base32::Alphabet;
use clap::{ArgEnum, Parser};
use totp_lite::{Sha1, Sha256, Sha512, totp_custom};

#[derive(Parser)]
#[clap(author, version, about, long_about = None)]
struct Args {
    #[clap(help = "Secret key for TOTP Token")]
    key: String,

    #[clap(arg_enum, short, long, default_value_t = Algorithm::Sha1, help = "Algorithm used to generate token")]
    algorithm: Algorithm,

    #[clap(short, long, default_value_t = 6, help = "Number of digits in token")]
    digits: u32,

    #[clap(short, long, default_value_t = 30, help = "How long a token is valid in seconds")]
    period: u64,
}

#[derive(Copy, Clone, PartialEq, Eq, PartialOrd, Ord, ArgEnum)]
enum Algorithm {
    Sha1,
    Sha256,
    Sha512,
}

fn main() {
    let args = Args::parse();
    let mut key: Vec<u8> = vec![];
    match base32::decode(Alphabet::RFC4648 {padding: false}, args.key.as_str()) {
        None => {
            eprintln!("Failed to decode secret.");
            std::process::exit(-1);
        }
        Some(b) => {
            key.clone_from(&b);
        }
    };
    let key = key.as_slice();

    let seconds: u64 = SystemTime::now().duration_since(UNIX_EPOCH).unwrap().as_secs();
    let result = match args.algorithm {
        Algorithm::Sha1 => totp_custom::<Sha1>(args.period, args.digits, key, seconds),
        Algorithm::Sha256 => totp_custom::<Sha256>(args.period, args.digits, key, seconds),
        Algorithm::Sha512 => totp_custom::<Sha512>(args.period, args.digits, key, seconds),
    };
    print!("{}", result);
}
